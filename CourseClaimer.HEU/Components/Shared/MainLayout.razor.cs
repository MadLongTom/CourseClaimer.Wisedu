using BootstrapBlazor.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace CourseClaimer.HEU.Components.Shared
{
    /// <summary>
    /// 
    /// </summary>
    public sealed partial class MainLayout
    {
        private bool UseTabSet { get; set; } = false;

        private string Theme { get; set; } = "";

        private bool IsOpen { get; set; }

        private bool IsFixedHeader { get; set; } = true;

        private bool IsFixedFooter { get; set; } = true;

        private bool IsFullSide { get; set; } = false;

        private bool ShowFooter { get; set; } = true;

        private List<MenuItem>? Menus { get; set; }

        /// <summary>
        /// OnInitialized 方法
        /// </summary>
        protected override void OnInitialized()
        {
            base.OnInitialized();

            Menus = GetIconSideMenuItems();
        }

        private static List<MenuItem> GetIconSideMenuItems()
        {
            var menus = new List<MenuItem>
            {
                new() { Text = "Index", Icon = "fa-solid fa-fw fa-flag", Url = "/" , Match = NavLinkMatch.All},
                new() { Text = "添加账号", Icon = "fa-solid fa-fw fa-table", Url = "/table" },
                new() { Text = "选课记录", Icon = "fa-solid fa-fw fa-check-square", Url = "/tableRecord" },
                new() { Text = "账号日志", Icon = "fa-solid fa-fw fa-check-square", Url = "/tableEntity" }
            };

            return menus;
        }
    }
}
