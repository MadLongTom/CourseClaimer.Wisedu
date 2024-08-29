using System.ComponentModel;

#pragma warning  disable CS8618
namespace CourseClaimer.Wisedu.Shared.Dto
{
    public class RowDto
    {
        [DisplayName("教学班ID")]
        public string JXBID { get; set; }
        [DisplayName("课程名")]
        public string KCM { get; set; }
        [DisplayName("类别")]
        public string XGXKLB { get; set; }
    }
}
