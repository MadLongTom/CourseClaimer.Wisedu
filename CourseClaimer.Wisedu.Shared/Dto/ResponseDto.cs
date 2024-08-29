using System.Net;

namespace CourseClaimer.Wisedu.Shared.Dto
{
    public class ResponseDto<T> where T : class
    {
        public HttpStatusCode StatusCode { get; set; }
        public HttpStatusCode InnerCode { get; set; }
        public string InnerMessage { get; set; }
        public Exception? Exception { get; set; }
        public string? RawResponse { get; set; }
        public bool IsSuccess { get; set; }

        public T Data { get; set; } = default!;

        public void EnsureSuccess()
        {
            if(!IsSuccess) throw new Exception($"Http Request Failed with StatusCode={StatusCode}{Environment.NewLine}{RawResponse!}{Environment.NewLine}{Exception!.Message}");
        }
    }
}
