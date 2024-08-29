namespace CourseClaimer.Wisedu.Shared.Models.Runtime
{
    public class WorkInfo
    {
        public Entity Entity { get; set; }
        public Task task { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    }
}
