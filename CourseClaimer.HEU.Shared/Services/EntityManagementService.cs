using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CourseClaimer.HEU.Shared.Dto;
using CourseClaimer.HEU.Shared.Models.Database;
using CourseClaimer.HEU.Shared.Models.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CourseClaimer.HEU.Shared.Services
{
    public class EntityManagementService(AuthorizeService authorizeService,ILogger<EntityManagementService> logger,ClaimDbContext dbContext,IServiceProvider serviceProvider) :IHostedService
    {
        public List<WorkInfo> WorkInfos { get; set; } = [];
        public async Task AddCustomer(string userName, string password, string categories, string course, bool isFinished)
        {
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                Password = password,
                Categories = categories,
                Course = course,
                IsFinished = isFinished
            };
            await dbContext.Customers.AddAsync(customer);
            await dbContext.SaveChangesAsync();
            await RefreshCustomerStatus(customer);
        }
        public async Task DeleteCustomer(string userName)
        {
            var customer = await dbContext.Customers.FirstAsync(c => c.UserName == userName);
            customer.IsFinished = true;
            await RefreshCustomerStatus(customer);
            dbContext.Customers.Remove(customer);
            await dbContext.SaveChangesAsync();
        }
        public async Task<QueryDto<Customer>> QueryUser(int page,int pageSize)
        {
            var query = dbContext.Customers.AsQueryable();
            var total = await query.CountAsync();
            var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return new QueryDto<Customer>
            {
                Total = total,
                Data = data
            };
        }
        public async Task<QueryDto<ClaimRecord>> QueryRecord(int page, int pageSize)
        {
            var query = dbContext.ClaimRecords.AsQueryable();
            var total = await query.CountAsync();
            var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return new QueryDto<ClaimRecord>
            {
                Total = total,
                Data = data
            };
        }
        public async Task EditCustomer(string userName, string password, string categories, string course, bool isFinished)
        {
            var customer = await dbContext.Customers.FirstAsync(c => c.UserName == userName);
            customer.Password = password;
            customer.Categories = categories;
            customer.Course = course;
            customer.IsFinished = isFinished;
            await dbContext.SaveChangesAsync();
            await RefreshCustomerStatus(customer);
        }

        public async Task RefreshCustomerStatus(Customer customer)
        {
            var workinfo = WorkInfos.FirstOrDefault(w => w.Entity.username == customer.UserName);
            if (!customer.IsFinished)
            {
                if (workinfo == null)
                {
                    var entity = new Entity(customer.UserName, customer.Password, customer.Categories == string.Empty ? [] : customer.Categories.Split(',').ToList(), customer.Course == string.Empty ? [] : customer.Course.Split(',').ToList(), [],false,null);
                    var claimService = serviceProvider.GetRequiredService<ClaimService>();
                    var cts = new CancellationTokenSource();
                    await authorizeService.MakeUserLogin(entity);
                    var task = claimService.GetPrivateList(await claimService.GetAllList(entity), entity).Count > 2 ? claimService.QueryClaim(entity,cts.Token) : claimService.DirectClaim(entity, cts.Token);
                    WorkInfos.Add(new WorkInfo
                    {
                        Entity = entity,
                        ClaimService = claimService,
                        ClaimTask = task,
                        CancellationTokenSource = cts
                    });
                }
            }
            else
            {
                if (workinfo != null)
                {
                    await workinfo.CancellationTokenSource.CancelAsync();
                    WorkInfos.Remove(workinfo);
                }
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var customers = await dbContext.Customers.ToListAsync(cancellationToken);
            foreach (var customer in customers)
            {
                await RefreshCustomerStatus(customer);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var workInfo in WorkInfos)
            {
                await workInfo.CancellationTokenSource.CancelAsync();
            }
        }
    }
}
