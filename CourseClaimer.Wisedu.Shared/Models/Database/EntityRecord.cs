using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BootstrapBlazor.Components;

namespace CourseClaimer.Wisedu.Shared.Models.Database
{
    public class EntityRecord
    {
        [Key]
        [AutoGenerateColumn(IsVisibleWhenAdd = false, IsVisibleWhenEdit = false)]
        public Guid Id { get; set; } = Guid.NewGuid();
        [DisplayName("事件时间")]
        public DateTime Time { get; set; } = DateTime.Now;
        [DisplayName("用户")]
        public string UserName { get; set; }
        [DisplayName("日志信息")]
        public string Message { get; set; }
    }
}
