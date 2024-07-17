using CourseClaimer.HEU.Shared.Enums;
using CourseClaimer.HEU.Shared.Extensions;
using CourseClaimer.HEU.Shared.Models.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CourseClaimer.HEU.Shared.Services
{
    public class ClaimService(ILogger<ClaimService> logger,AuthorizeService authorizeService,ClaimDbContext dbContext)
    {
        public async Task MakeUserFinished(Entity entity)
        {
            var customer = await dbContext.Customers.FirstAsync(c => c.UserName == entity.username);
            customer.IsFinished = true;
            await dbContext.SaveChangesAsync();
        }

        public async Task LogClaimRecord(Entity entity, Row @class,bool success)
        {
            dbContext.ClaimRecords.Add(new ClaimRecord()
            {
                IsSuccess = success,
                UserName = entity.username,
                Course = @class.KCM + @class.XGXKLB,
            });
            await dbContext.SaveChangesAsync();
        }
        public async Task<List<Row>> GetAvailableList(Entity entity)
        {
            var res = await entity.GetRowList().ToResponseDto<ListRoot>();
            if (!res.IsSuccess) return [];
            var availableRows = res.Data.data.rows.Where(q => q.classCapacity > q.numberOfSelected);
            if (availableRows.Count() != 0)
                logger.LogInformation($"AvailableList:{entity.username} found available course {string.Join('|', availableRows.Select(c => c.KCM))}");
            return availableRows.ToList();
        }

        public async Task<List<Row>> GetAllList(Entity entity)
        {
            var res = await entity.GetRowList().ToResponseDto<ListRoot>();
            logger.LogInformation($"AllList:{entity.username} found available course {string.Join('|', res.Data.data.rows.Select(c => c.KCM))}");
            return res.IsSuccess ? res.Data.data.rows.ToList() : [];
        }

        public async Task<AddResult> Add(Entity entity,Row @class)
        {
            var res = await entity.Add(@class).ToResponseDto<AddRoot>();
            if (res.IsSuccess)
            {
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
                dbContext.EntityRecords.Add(new EntityRecord()
                {
                    UserName = entity.username,
                    Message = $"Add:{entity.username} when claiming {@class.KCM}, server reported {res.InnerMessage}"
                });
                await dbContext.SaveChangesAsync();
                return AddResult.UnknownError;
            }
            return AddResult.UnknownError;
        }

        public async Task<ValidateResult> ValidateClaim(Entity entity,Row @class)
        {
            var res = await entity.ValidateClaim(@class).ToResponseDto<SelectRoot>();
            if (res.IsSuccess)
            {
                return res.Data.data.Any(q => q.KCH == @class.KCH) ? ValidateResult.Success : ValidateResult.Miss;
            }
            return ValidateResult.UnknownError;
        }

        public async Task QueryClaim(Entity entity,CancellationToken cancellationToken = default)
        {
            while (true)
            {
                if (entity.finished || cancellationToken.IsCancellationRequested) return;
                List<Row> publicList = await GetAvailableList(entity);
                List<Row> privateList = GetPrivateList(publicList, entity);
                privateList = privateList.Where(p => p.classCapacity > p.numberOfSelected).ToList();
                if (privateList.Count <= 0) continue;
                foreach (Row @class in privateList)
                {
                    if(cancellationToken.IsCancellationRequested) return;
                    await Claim(entity, @class);
                }
            }
        }
        public async Task DirectClaim(Entity entity, CancellationToken cancellationToken = default)
        {
            List<Row> publicList = await GetAllList(entity);
            List<Row> privateList = GetPrivateList(publicList, entity);
            while (true)
            {
                privateList.RemoveAll(q => entity.done.Any(p => p.KCH == q.KCH));
                if (privateList.Count == 0) return;
                foreach (Row @class in privateList)
                {
                    if (entity.finished || cancellationToken.IsCancellationRequested) return;
                    await Claim(entity, @class);
                }
            }
        }

        public async Task Claim(Entity entity, Row @class)
        {
            while (true)
            {
                var res = await Add(entity, @class);
                switch (res)
                {
                    case AddResult.Success:
                        var validate = await ValidateClaim(entity, @class);

                        await LogClaimRecord(entity, @class, validate == ValidateResult.Success);

                        if (validate != ValidateResult.Success) return;
                        entity.done.Add(@class);
                        if (entity.done.Count < 5) return;

                        await MakeUserFinished(entity);

                        entity.finished = true;
                        return;
                    case AddResult.OverSpeed:
                        await Task.Delay(50);
                        continue;
                    case AddResult.Full:
                        entity.finished = true;

                        await MakeUserFinished(entity);

                        return;
                    case AddResult.Failed:
                        logger.LogInformation($"Claim:{entity.username} Failed to claim {@class.KCM}");
                        await LogClaimRecord(entity, @class, false);
                        return;
                    case AddResult.Conflict:
                        return;
                    case AddResult.AuthorizationExpired:
                        entity.finished = true;
                        await authorizeService.MakeUserLogin(entity);
                        return;
                    case AddResult.UnknownError:
                        await Task.Delay(1000);
                        return;
                }
                break;
            }
        }

        static readonly Dictionary<string, int> xgxklb = new() { { "A", 12 }, { "B", 13 }, { "C", 14 }, { "D", 15 }, { "E", 16 }, { "F", 17 }, { "A0", 18 } };
        public List<Row> GetPrivateList(List<Row> publicList, Entity entity)
        {
            List<Row> privateList = [];
            if (entity.classname.Count > 0 && entity.category.Count > 0)
            {
                privateList = publicList.Where(p => entity.classname.Any(q =>
                    p.KCM.Contains(q)) && entity.category.Contains(xgxklb.First(t => p.XGXKLB.Contains(t.Key)).Key)).ToList();
            }
            else if (entity.classname.Count > 0)
            {
                privateList.AddRange(publicList.Where(p => entity.classname.Any(q => p.KCM.Contains(q))));
            }
            else if (entity.category.Count > 0)
            {
                privateList.AddRange(publicList.Where(p => entity.category.Contains(xgxklb.First(t => p.XGXKLB.Contains(t.Key)).Key)));
            }
            else
            {
                privateList = publicList;
            }
            privateList.RemoveAll(q => entity.done.Any(p => p.KCH == q.KCH));
            logger.LogInformation($"PrivateList:{entity.username} found available course {string.Join('|', privateList.Select(c => c.KCM))}");
            return privateList;
        }

    }
}
