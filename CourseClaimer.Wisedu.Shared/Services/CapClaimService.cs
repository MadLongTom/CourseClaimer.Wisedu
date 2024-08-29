using CourseClaimer.Wisedu.Shared.Extensions;
using CourseClaimer.Wisedu.Shared.Models.JWXK;
using CourseClaimer.Wisedu.Shared.Models.Runtime;
using DotNetCore.CAP;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CourseClaimer.Wisedu.Shared.Services
{
    public class CapClaimService(ILogger<CapClaimService> logger, ClaimService claimService,IConfiguration configuration) : ICapSubscribe
    {
        private int takeNum = Convert.ToInt32(configuration["CapTakeNum"]); 

        [CapSubscribe("ClaimService.RowAvailable")]
        public async Task CapClaimRow(Row row)
        {
            foreach (var entity in ProgramExtensions.Entities.Where(entity =>
                         entity.SubscribedRows.Contains(row.JXBID) && !entity.IsAddPending)
                         .OrderByDescending(e => e.priority)
                         .Take(takeNum))
            {
                _ = claimService.Claim(entity, row);
                logger.LogInformation($"CapClaimRow:{entity.username} is claiming {row.KCM}");
            }
        }

        public async Task StartAsync(Entity entity, CancellationToken token = default)
        {
            var thisRows = await claimService.GetAllList(entity);
            if (Convert.ToBoolean(configuration["LegacyMode"]))
            {
                while(!token.IsCancellationRequested)
                {
                    foreach (var row in entity.SubscribedRows)
                    {
                        await claimService.Claim(entity, thisRows.Find(r => r.JXBID == row));
                    }
                }
            }
            else
            {
                while (!token.IsCancellationRequested)
                {
                    await claimService.GetSubscriptionList(entity);
                }
            }
        }
    }
}
