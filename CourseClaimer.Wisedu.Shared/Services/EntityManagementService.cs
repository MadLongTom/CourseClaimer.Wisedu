using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using BootstrapBlazor.Components;
using CourseClaimer.Wisedu.Shared.Dto;
using CourseClaimer.Wisedu.Shared.Enums;
using CourseClaimer.Wisedu.Shared.Extensions;
using CourseClaimer.Wisedu.Shared.Models.Database;
using CourseClaimer.Wisedu.Shared.Models.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CourseClaimer.Wisedu.Shared.Services
{
    public class EntityManagementService(
        AuthorizeService authorizeService,
        CapClaimService capClaimService,
        ILogger<EntityManagementService> logger,
        ClaimDbContext dbContext) : IHostedService
    {
        public List<WorkInfo> WorkInfos { get; set; } = [];

        public async Task<(string,string)> ExportAllCustomer()
        {
            var customers = await dbContext.Customers.AsNoTracking().ToListAsync();
            foreach (var customer in customers)
            {
                customer.Id = Guid.Empty;
            }
            //save to file
            var path = Path.Combine(Directory.GetCurrentDirectory(), $"customers-{DateTime.Now.ToFileTime()}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(customers));
            return (JsonSerializer.Serialize(customers), path);
        }

        public async Task<string> ExportAllClaims()
        {
            var claims = await dbContext.ClaimRecords.AsNoTracking()
                .Where(c => c.IsSuccess == true)
                .OrderBy(x => x.UserName)
                .ThenBy(x => x.ClaimTime)
                .ToListAsync();
            return JsonSerializer.Serialize(claims);
        }

        public async Task AddCustomersFromJson(string json)
        {
            var customers = JsonSerializer.Deserialize<List<Customer>>(json);
            //remove exists customers
            var exists = await dbContext.Customers.Select(c => c.UserName).ToListAsync();
            customers.RemoveAll(c => exists.Contains(c.UserName));
            await dbContext.Customers.AddRangeAsync(customers);
            await dbContext.SaveChangesAsync();
        }

        public async Task<bool> AddCustomer(Customer customer)
        {
            //return if user already exists
            if (await dbContext.Customers.AnyAsync(c => c.UserName == customer.UserName))
            {
                return false;
            }
            await dbContext.Customers.AddAsync(customer);
            await dbContext.SaveChangesAsync();
            await RefreshCustomerStatus(customer);
            return true;
        }

        public async Task DeleteCustomer(string userName)
        {
            var customer = await dbContext.Customers.FirstAsync(c => c.UserName == userName);
            customer.IsFinished = true;
            await RefreshCustomerStatus(customer);
            dbContext.Customers.Remove(customer);
            await dbContext.SaveChangesAsync();
        }

        public async Task <QueryDto<T>> Query<T>(QueryPageOptions options) where T : class
        {
            var query = dbContext.Set<T>().AsNoTracking().Where(options.GetSearchFilter<T>(FilterLogic.Or));
            var total = query.Count();
            var data = query.Skip((options.PageIndex - 1) * options.PageItems).Take(options.PageItems).ToList();
            return new QueryDto<T>
            {
                Total = total,
                Data = data
            };
        }

        public async Task <QueryDto<RowDto>> QueryRow(QueryPageOptions options)
        {
            var query = ProgramExtensions.AllRows.Where(options.GetSearchFilter<RowDto>(FilterLogic.Or)).OrderBy(row => row.XGXKLB);
            return new QueryDto<RowDto>
            {
                Total = query.Count(),
                Data = query.Skip((options.PageIndex - 1) * options.PageItems).Take(options.PageItems).ToList()
            };
        }

        public async Task EditCustomer(Customer customer)
        {
            var local = dbContext.Set<Customer>()
                .Local
                .FirstOrDefault(entry => entry.Id.Equals(customer.Id));
            if (local != null)
            {
                dbContext.Entry(local).State = EntityState.Detached;
            }
            dbContext.Entry(customer).State = EntityState.Modified;
            await dbContext.SaveChangesAsync();
            await RefreshCustomerStatus(customer);
        }

        static readonly FrozenDictionary<string, string> xgxklbs = FrozenDictionary.ToFrozenDictionary(new Dictionary<string, string>
        {
            { "A", "19人文素质与文化传承（A）" }, { "B", "19艺术鉴赏与审美体验（B）" }, { "C", "19社会发展与公民责任（C）" }, { "D", "19自然科学与工程技术（D）" },
            { "E", "19三海一核与国防建设（E）" }, { "F", "19创新思维与创业实践（F）" }, { "A0", "19中华传统文化类（A0）" }
        });

        public async Task RefreshCustomerStatus(Customer customer)
        {
            var workinfo = WorkInfos.FirstOrDefault(w => w.Entity.username == customer.UserName);
            if (!customer.IsFinished)
            {
                if (workinfo == null)
                {
                    try
                    {
                        var entity = new Entity(customer.UserName, customer.Password,
                                                customer.Categories == string.Empty ? [] : customer.Categories.Split(',').Select(p => xgxklbs[p]).ToList(),
                                                customer.Course == string.Empty ? [] : [.. customer.Course.Split(',')],
                                                [], false, null, customer.Priority);
                        ProgramExtensions.Entities.Add(entity);
                        var cts = new CancellationTokenSource();
                        LoginResult loginResult;
                        do
                        {
                            loginResult = await authorizeService.MakeUserLogin(entity);
                        } while (loginResult == LoginResult.WrongCaptcha);

                        if (loginResult == LoginResult.WrongPassword)
                        {
                            logger.LogError($"Login:{entity.username}: Wrong Password");
                            dbContext.EntityRecords.Add(new EntityRecord()
                            {
                                UserName = customer.UserName,
                                Message = "Login: Wrong Password"
                            });
                            dbContext.Customers.Remove(customer);
                            await dbContext.SaveChangesAsync();
                            return;
                        }

                        var task = capClaimService.StartAsync(entity, cts.Token);

                        WorkInfos.Add(new WorkInfo
                        {
                            Entity = entity,
                            task = task,
                            CancellationTokenSource = cts
                        });

                    }
                    catch(Exception ex)
                    {
                        logger.LogError($"Error while refreshing status {customer.UserName}: ", ex);
                    }
                    
                }
            }
            else
            {
                if (workinfo != null)
                {
                    await workinfo.CancellationTokenSource.CancelAsync();
                    ProgramExtensions.Entities.Remove(workinfo.Entity);
                    WorkInfos.Remove(workinfo);
                }
            }
        }

        public async Task WebStartAsync(CancellationToken cancellationToken = default)
        {
            var customers = await dbContext.Customers.OrderByDescending(c => c.Priority).ToListAsync(cancellationToken);
            foreach (var customer in customers)
            {
                customer.IsFinished = false;
                await dbContext.SaveChangesAsync(cancellationToken);
                try
                {
                    await RefreshCustomerStatus(customer);
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, $"WebStartAsync:{customer.UserName}");
                }
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var customers = await dbContext.Customers.ToListAsync(cancellationToken);
            foreach (var customer in customers)
            {
                customer.IsFinished = true;
                await dbContext.SaveChangesAsync(cancellationToken);
                await RefreshCustomerStatus(customer);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            foreach (var workInfo in WorkInfos)
            {
                await workInfo.CancellationTokenSource.CancelAsync();
            }
            WorkInfos.Clear();
            ProgramExtensions.Entities.Clear();
        }
    }
}
