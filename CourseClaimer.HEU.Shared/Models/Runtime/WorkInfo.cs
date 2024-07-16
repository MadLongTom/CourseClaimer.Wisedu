using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CourseClaimer.HEU.Shared.Services;

namespace CourseClaimer.HEU.Shared.Models.Runtime
{
    public class WorkInfo
    {
        public Entity Entity { get; set; }
        public ClaimService ClaimService { get; set; }
        public Task ClaimTask { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    }
}
