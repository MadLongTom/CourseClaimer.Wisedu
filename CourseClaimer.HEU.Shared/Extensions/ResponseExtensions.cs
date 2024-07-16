using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using CourseClaimer.HEU.Shared.Dto;
using CourseClaimer.HEU.Shared.Models.JWXK.Roots;

namespace CourseClaimer.HEU.Shared.Extensions
{
    public static class ResponseExtensions
    {
        public static async Task<ResponseDto<T>> ToResponseDto<T>(this HttpResponseMessage message,Exception? exception = null) where T : BaseRoot
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
                response.InnerMessage = response.Data?.msg ?? "";
            }
            else
            {
                response.RawResponse = await message.Content.ReadAsStringAsync();
                response.Exception = exception ?? new Exception("Http Request Failed");
            }
            return response;
        }
        public static async Task<ResponseDto<T>> ToResponseDto<T>(this Task<HttpResponseMessage> request) where T : BaseRoot
        {
            try
            {
                var message = await request;
                return await message.ToResponseDto<T>();
            }
            catch (Exception ex)
            {
                return new ResponseDto<T>
                {
                    Exception = ex,
                    IsSuccess = false
                };
            }
        }
    }
}
