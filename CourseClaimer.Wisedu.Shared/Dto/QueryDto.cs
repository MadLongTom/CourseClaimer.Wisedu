namespace CourseClaimer.Wisedu.Shared.Dto
{
    public class QueryDto<T> where T: class
    {
        public int Total { get; set; }
        public List<T> Data { get; set; }
    }
}
