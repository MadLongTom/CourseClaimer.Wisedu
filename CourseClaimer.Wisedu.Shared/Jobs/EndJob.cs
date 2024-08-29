using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CourseClaimer.Wisedu.Shared.Models.Database;
using CourseClaimer.Wisedu.Shared.Services;
using Microsoft.Extensions.Configuration;
using Quartz;

namespace CourseClaimer.Wisedu.Shared.Jobs
{
    public class EndJob(EntityManagementService EMS, ClaimDbContext dbContext, IConfiguration configuration) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            dbContext.JobRecords.Add(new JobRecord()
            {
                JobName = "EndJob",
                Message = "开始执行",
            });
            await dbContext.SaveChangesAsync();
            await EMS.StopAsync();
            dbContext.JobRecords.Add(new JobRecord()
            {
                JobName = "ReconnectJob",
                Message = "关闭所有",
            });
            await dbContext.SaveChangesAsync();
        }
    }
}
