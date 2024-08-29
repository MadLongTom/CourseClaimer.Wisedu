using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace CourseClaimer.Wisedu.Shared.Models.Database;

public class JobRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [DisplayName("执行时间")]
    public DateTime ExecuteTime { get; set; }=DateTime.Now;
    [DisplayName("任务")]
    public string JobName { get; set; }
    [DisplayName("信息")]
    public string Message { get; set; }
}