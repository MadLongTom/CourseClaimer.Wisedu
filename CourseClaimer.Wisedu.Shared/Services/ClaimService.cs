using System.Linq;
using System.Net;
using CourseClaimer.Wisedu.Shared.Dto;
using CourseClaimer.Wisedu.Shared.Enums;
using CourseClaimer.Wisedu.Shared.Extensions;
using CourseClaimer.Wisedu.Shared.Models.Database;
using CourseClaimer.Wisedu.Shared.Models.JWXK.Roots;
using CourseClaimer.Wisedu.Shared.Models.Runtime;
using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Row = CourseClaimer.Wisedu.Shared.Models.JWXK.Row;

namespace CourseClaimer.Wisedu.Shared.Services
{
    public class ClaimService(
        ILogger<ClaimService> logger,
        AuthorizeService authorizeService,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ICapPublisher capBus)
    {
        public async Task MakeUserFinished(Entity entity)
        {
            using var dbContext = serviceProvider.GetRequiredService<ClaimDbContext>();
            var customer = await dbContext.Customers.FirstAsync(c => c.UserName == entity.username);
            dbContext.Customers.Remove(customer);
            dbContext.EntityRecords.Add(new EntityRecord()
            {
                UserName = entity.username,
                Message = "MakeUserFinished: User has claimed 5 courses or subscription is null"
            });
            await dbContext.SaveChangesAsync();
        }

        public async Task LogEntityRecord(Entity entity, string message)
        {
            logger.LogWarning($"Possibly confronted anti-peek-shaving!{Environment.NewLine}{message}");
            using var dbContext = serviceProvider.GetRequiredService<ClaimDbContext>();
            dbContext.EntityRecords.Add(new EntityRecord()
            {
                UserName = entity.username,
                Message = message
            });
            await dbContext.SaveChangesAsync();
            LoginResult loginResult;
            do loginResult = await authorizeService.MakeUserLogin(entity);
            while (loginResult == LoginResult.WrongCaptcha);
            if (loginResult == LoginResult.WrongPassword)
            {
                await MakeUserFinished(entity);
                entity.finished = true;
            }
        }

        public async Task LogClaimRecord(Entity entity, Row @class, bool success)
        {
            if (!success && Convert.ToBoolean(configuration["LegacyMode"])) return;
            using var dbContext = serviceProvider.GetRequiredService<ClaimDbContext>();
            var customer = dbContext.Customers.AsNoTracking().FirstOrDefault(c => c.UserName == entity.username);
            dbContext.ClaimRecords.Add(new ClaimRecord()
            {
                IsSuccess = success,
                UserName = entity.username,
                Course = @class.KCM,
                Category = @class.XGXKLB,
                Tenant = customer?.Tenant ?? string.Empty,
                Contact = customer?.Contact ?? string.Empty,
            });
            await dbContext.SaveChangesAsync();
        }

        public async Task<List<Row>> GetSubscriptionList(Entity entity)
        {
            var res = await entity.GetRowList(true).ToResponseDto<ListRoot>();
            if (res.Exception != null)
            {
                await LogEntityRecord(entity,
                    $@"GetRowList: {entity.username}: Unexpected Result: {res.Exception.Message}{Environment.NewLine}{res.RawResponse}");
                return [];
            }

            if (res.InnerMessage.Contains("请重新登录"))
            {
                LoginResult loginResult;
                do loginResult = await authorizeService.MakeUserLogin(entity, true);
                while (loginResult == LoginResult.WrongCaptcha);
                if (loginResult == LoginResult.WrongPassword) entity.finished = true;
            }

            if (!res.IsSuccess) return [];
            var availableRows = res.Data.data.rows.Where(q => q.classCapacity > q.numberOfSelected);
            //if (availableRows.Count() != 0)
            //    logger.LogInformation($"AvailableList:{entity.username} found available course {string.Join('|', availableRows.Select(c => c.KCM))}");
            foreach (var row in availableRows)
            {
                await capBus.PublishAsync("ClaimService.RowAvailable", row);
                logger.LogInformation($"GetSubscriptionList:{entity.username} found available courses: {row.KCM}");
                var secret = entity.Secrets.FirstOrDefault(s => s.JXBID == row.JXBID);
                if (secret is null)
                {
                    entity.Secrets.Add(new RowSecretDto
                    {
                        JXBID = row.JXBID,
                        secretVal = row.secretVal,
                        classId = row.JXBID
                    });
                }
                if (secret.secretVal != row.secretVal)
                {
                    secret.secretVal = row.secretVal;
                    secret.classId = row.JXBID;
                }
            }
            return availableRows.ToList();
        }

        public async Task<List<Row>> GetAllList(Entity entity)
        {
            var res = await entity.GetRowList().ToResponseDto<ListRoot>();
            if (res.Exception != null)
            {
                await LogEntityRecord(entity,
                    $@"GetRowList: {entity.username}: Unexpected Result: {res.Exception.Message}{Environment.NewLine}{res.RawResponse}");
                return [];
            }

            if (res.InnerMessage.Contains("请重新登录"))
            {
                LoginResult loginResult;
                do loginResult = await authorizeService.MakeUserLogin(entity, true);
                while (loginResult == LoginResult.WrongCaptcha);
                if (loginResult == LoginResult.WrongPassword) entity.finished = true;
            }

            if (!res.IsSuccess) return [];
            foreach (var row in res.Data.data.rows.Where(r => ProgramExtensions.AllRows.All(ar => ar.JXBID != r.JXBID)))
            {
                ProgramExtensions.AllRows.Add(new() { JXBID = row.JXBID, KCM = row.KCM, XGXKLB = row.XGXKLB });
            }

            entity.SubscribedRows.AddRange(res.Data.data.rows.Where(row =>
                (entity.courses.Count == 0 || entity.courses.Any(c => row.KCM.Contains(c))))
                .Where(row => (entity.category.Count == 0 || entity.category.Any(c => c == row.XGXKLB)))
                .Where(row => !ProgramExtensions.ExceptionList.Contains(row.KCM))
                .Select(row => row.JXBID));
           
            entity.Secrets.AddRange(res.Data.data.rows.Select(row => new RowSecretDto
            {
                JXBID = row.JXBID,
                secretVal = row.secretVal,
                classId = row.JXBID
            }));

            var claimedRows = await GetClaimedRows(entity);

            entity.SubscribedRows.RemoveAll(claimedRows.Contains);

            if (claimedRows.Count >= 5 || entity.SubscribedRows.Count == 0)
            {
                await MakeUserFinished(entity);
                entity.finished = true;
                return [];
            }

            return res.IsSuccess ? res.Data.data.rows.ToList() : [];
        }

        public async Task<List<string>> GetClaimedRows(Entity entity)
        {
            var res = await entity.ValidateClaim().ToResponseDto<SelectRoot>();
            if (res.Exception != null)
            {
                await LogEntityRecord(entity,
                    $@"Validate: {entity.username}: Unexpected Result: {res.Exception.Message}{Environment.NewLine}{res.RawResponse}");
            }
            if (res.IsSuccess)
            {
                var JXBIDList = res.Data.data.Select(c => c.JXBID);
                var KCMList = res.Data.data.Select(c => c.KCM);
                var dbContext = serviceProvider.GetRequiredService<ClaimDbContext>();
                foreach (var KCM in KCMList)
                {
                    if (!dbContext.ClaimRecords
                            .Where(c => c.UserName == entity.username)
                            .Where(c => c.Course.Contains(KCM))
                            .Any(c => c.IsSuccess == true))
                    {
                        var row = res.Data.data.First(c => c.KCM == KCM);
                        var customer = dbContext.Customers.AsNoTracking().FirstOrDefault(c => c.UserName == entity.username);
                        dbContext.ClaimRecords.Add(new ClaimRecord()
                        {
                            Category = row.XGXKLB,
                            Course = row.KCM,
                            UserName = entity.username,
                            IsSuccess = true,
                            Tenant = customer?.Tenant ?? string.Empty,
                            Contact = customer?.Contact ?? string.Empty,
                        });
                        await dbContext.SaveChangesAsync();
                    }
                }
                foreach (var row in dbContext.ClaimRecords
                             .Where(c => c.UserName == entity.username)
                             .Where(c => c.IsSuccess == true))
                {
                    if (!KCMList.Any(k => row.Course.Contains(k)))
                    {
                        row.IsSuccess = false;
                    }
                }
                await dbContext.SaveChangesAsync();
                return JXBIDList.ToList();
            }
            return [];
        }

        public async Task<AddResult> Add(Entity entity, Row @class)
        {
            var res = await entity.Add(@class).ToResponseDto<AddRoot>();
            if (res.Exception != null)
            {
                await LogEntityRecord(entity,
                    $@"Add: {entity.username}: Unexpected Result: {res.Exception.Message}{Environment.NewLine}{res.RawResponse}");
                return AddResult.UnknownError;
            }
            if (res.InnerCode == HttpStatusCode.OK)
            {
                logger.LogInformation($"Add:{entity.username} has claimed {@class.KCM}({@class.XGXKLB})");
                entity.done.Add(@class);
                return AddResult.Success;
            }
            if (res.InnerMessage.Contains("请求过快")) return AddResult.OverSpeed;
            if (res.InnerMessage.Contains("已选满5门，不可再选") || res.InnerMessage.Contains("学分超过"))
                return AddResult.Full;
            if (res.InnerMessage.Contains("容量已满")) return AddResult.Failed;
            if (res.InnerMessage.Contains("选课结果中") || res.InnerMessage.Contains("不能重复选课") ||
                res.InnerMessage.Contains("冲突")) return AddResult.Conflict;
            if (res.InnerMessage.Contains("请重新登录")) return AddResult.AuthorizationExpired;
            logger.LogWarning($"Add:{entity.username} when claiming {@class.KCM}, server reported {res.InnerMessage}");
            var dbContext = serviceProvider.GetRequiredService<ClaimDbContext>();
            dbContext.EntityRecords.Add(new EntityRecord()
            {
                UserName = entity.username,
                Message = $"Add:{entity.username} when claiming {@class.KCM}, server reported {res.InnerMessage}"
            });
            await dbContext.SaveChangesAsync();
            return AddResult.UnknownError;
        }

        public async Task<ValidateResult> ValidateClaim(Entity entity, Row @class)
        {
            var res = await entity.ValidateClaim().ToResponseDto<SelectRoot>();
            if (res.Exception != null)
            {
                await LogEntityRecord(entity,
                    $@"Validate: {entity.username}: Unexpected Result: {res.Exception.Message}{Environment.NewLine}{res.RawResponse}");
                return ValidateResult.UnknownError;
            }
            if (res.IsSuccess)
            {
                return res.Data.data.Any(q => q.JXBID == @class.JXBID) ? ValidateResult.Success : ValidateResult.Miss;
            }
            return ValidateResult.UnknownError;
        }

        public async Task Claim(Entity entity, Row @class)
        {
            entity.IsAddPending = true;
            while (true)
            {
                var res = await Add(entity, @class);
                switch (res)
                {
                    case AddResult.Success:
                        ValidateResult validate;
                        do validate = await ValidateClaim(entity, @class);
                        while(validate == ValidateResult.UnknownError);
                        await LogClaimRecord(entity, @class, validate == ValidateResult.Success);
                        entity.IsAddPending = false;
                        if (validate != ValidateResult.Success) return;
                        entity.done.Add(@class);
                        if (entity.done.Count < 5) return;
                        await MakeUserFinished(entity);
                        entity.finished = true;
                        return;
                    case AddResult.OverSpeed:
                        {
                            using var dbContext = serviceProvider.GetRequiredService<ClaimDbContext>();
                            logger.LogWarning($"Claim:{entity.username} OverSpeed when claiming {@class.KCM}");
                            dbContext.EntityRecords.Add(new EntityRecord()
                            {
                                UserName = entity.username,
                                Message = $"Claim:{entity.username} OverSpeed when claiming {@class.KCM}"
                            });
                            await dbContext.SaveChangesAsync();
                            continue;
                        }
                    case AddResult.Full:
                        entity.IsAddPending = false;
                        entity.finished = true;
                        await MakeUserFinished(entity);
                        return;
                    case AddResult.Failed:
                        entity.IsAddPending = false;
                        logger.LogInformation($"Claim:{entity.username} Failed to claim {@class.KCM}");
                        await LogClaimRecord(entity, @class, false);
                        return;
                    case AddResult.Conflict:
                        entity.IsAddPending = false;
                        entity.SubscribedRows.Remove(@class.JXBID);
                        return;
                    case AddResult.AuthorizationExpired:
                        entity.IsAddPending = true;
                        LoginResult loginResult;
                        do loginResult = await authorizeService.MakeUserLogin(entity,true);
                        while (loginResult == LoginResult.WrongCaptcha);
                        if (loginResult == LoginResult.WrongPassword) entity.finished = true;
                        entity.IsAddPending = false;
                        return;
                    case AddResult.UnknownError:
                        entity.IsAddPending = false;
                        return;
                }
                break;
            }
        }

    }
}
