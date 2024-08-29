using BootstrapBlazor.Components;
using CourseClaimer.Wisedu.Shared.Dto;
using CourseClaimer.Wisedu.Shared.Models.Database;
using CourseClaimer.Wisedu.Shared.Models.Runtime;
using Microsoft.Extensions.Options;

namespace CourseClaimer.Wisedu.Shared.Extensions
{
    public static class ProgramExtensions
    {
        public static List<RowDto> AllRows { get; set; } = [];
        public static List<Entity> Entities { get; set; } = [];
        public static List<string> ExceptionList { get; set; } = [];

        public static Func<T, bool> GetSearchFilter<T>(this QueryPageOptions option, FilterLogic logic)
        {
            return option.Searches.Count != 0 ? option.Searches.GetFilterFunc<T>(logic) : option.AdvanceSearches.GetFilterFunc<T>(logic);
        }
    }
}
