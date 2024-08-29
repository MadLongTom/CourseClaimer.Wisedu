using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BootstrapBlazor.Components;

namespace CourseClaimer.Wisedu.Shared.Models.Database
{
    public class Customer
    {
        [Key]
        [AutoGenerateColumn(IsVisibleWhenAdd = false, IsVisibleWhenEdit = false)]
        public Guid Id { get; set; } = Guid.NewGuid();
        [DisplayName("用户名")]
        public string UserName { get; set; }
        [DisplayName("密码")]
        public string Password { get; set; }
        [DisplayName("类别")]
        public string Categories { get; set; } = string.Empty;
        [DisplayName("课程")]
        public string Course { get; set; } = string.Empty;
        [DisplayName("是否完成")]
        public bool IsFinished { get; set; }
        [DisplayName("优先级（越大越前）")] 
        public int Priority { get; set; } = 0;
        [DisplayName("代理商")] 
        public string Tenant { get; set; } = string.Empty;
        [DisplayName("联系方式")]
        public string Contact { get; set; } = string.Empty;

    }
}
