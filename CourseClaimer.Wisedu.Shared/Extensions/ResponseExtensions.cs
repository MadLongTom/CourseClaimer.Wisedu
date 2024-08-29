using System.Net;
using System.Net.Http.Json;
using CourseClaimer.Wisedu.Shared.Dto;
using CourseClaimer.Wisedu.Shared.Models.JWXK.Roots;

namespace CourseClaimer.Wisedu.Shared.Extensions
{
    public static class ResponseExtensions
    {
        public static async Task<ResponseDto<T>> ToResponseDto<T>(this HttpResponseMessage message,Exception? exception = null,bool dispose = true) where T : BaseRoot
        {
            var response = new ResponseDto<T>
            {
                StatusCode = message.StatusCode,
                IsSuccess = message.IsSuccessStatusCode
            };
            if (message.IsSuccessStatusCode)
            {
                response.Data = await message.Content.ReadFromJsonAsync<T>();
                response.InnerCode = (HttpStatusCode)(response.Data?.code ?? 400);
                response.IsSuccess = response.InnerCode == HttpStatusCode.OK;
                response.InnerMessage = response.Data?.msg ?? "";
            }
            else
            {
                response.RawResponse = await message.Content.ReadAsStringAsync();
                response.Exception = exception ?? new Exception("Http Request Failed");
            }
            if(dispose) message.Dispose();
            return response;
        }
        public static async Task<ResponseDto<T>> ToResponseDto<T>(this Task<HttpResponseMessage> request, bool dispose = true) where T : BaseRoot
        {
            var message = await request;
            try
            {
                return await message.ToResponseDto<T>(dispose:dispose);
            }
            catch (Exception ex)
            {
                return new ResponseDto<T>
                {
                    Exception = ex,
                    RawResponse = await message.Content.ReadAsStringAsync(),
                    IsSuccess = false
                };
            }
        }
    }
}
