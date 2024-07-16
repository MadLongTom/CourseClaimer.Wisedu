using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using CourseClaimer.HEU.Shared.Dto;

namespace CourseClaimer.HEU.Shared.Extensions
{
    public static class EntityExtensions
    {
        private const int LimitMillSeconds = 350;

        public static HttpRequestMessage BuildPostRequest(string url, Entity entity, MediaTypeHeaderValue? contentType, HttpContent content)
        {
            HttpRequestMessage hrt = new(HttpMethod.Post, url);
            hrt.Headers.Referrer = new Uri($"https://jwxk.hrbeu.edu.cn/xsxk/elective/grablessons?batchId={entity.batchId}");
            hrt.Headers.Host = "jwxk.hrbeu.edu.cn";
            hrt.Content = content;
            if (contentType != null) hrt.Content.Headers.ContentType = contentType;
            return hrt;
        }
        public static async Task DelayTillLimit(Stopwatch sw, int requiredMillSeconds)
        {
            if (sw.ElapsedMilliseconds < requiredMillSeconds)
            {
                await Task.Delay(Convert.ToInt32(requiredMillSeconds - sw.ElapsedMilliseconds));
            }
            else if (sw.ElapsedMilliseconds > requiredMillSeconds)
            {
                //TotalDelayTimeSpan += new TimeSpan(0, 0, 0, 0, Convert.ToInt32(sw.ElapsedMilliseconds - requiredMillSeconds));
                //NetworkDelayCounter += 1;
            }
        }
        public static async Task<HttpResponseMessage> LimitSendAsync(this HttpClient client, HttpRequestMessage hrm, Entity entity)
        {
            await DelayTillLimit(entity.stopwatch, LimitMillSeconds);
            entity.stopwatch.Restart();
            var res = await client.SendAsync(hrm);
            //Program.NetworkSendCounter += 1;
            //Program.TotalTimeSpan += entity.stopwatch.Elapsed;
            return res;
        }
        public static async Task<HttpResponseMessage> Captcha(this Entity entity)
        {
            var content = new FormUrlEncodedContent([]);
            content.Headers.ContentLength = 0;
            return await entity.client.PostAsync("https://jwxk.hrbeu.edu.cn/xsxk/auth/captcha", content);
        }

        public static async Task<HttpResponseMessage> Login(this Entity entity, string encodedPassword, string uuid, string auth)
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string> {
                {"loginname",entity.username },
                {"password", encodedPassword },
                {"captcha",auth },
                {"uuid",uuid }
            });
            content.Headers.ContentType = new("application/x-www-form-urlencoded");
            var res = entity.client.PostAsync("https://jwxk.hrbeu.edu.cn/xsxk/auth/hrbeu/login", content);
            return await res;
        }

        public static readonly string listUrl = "https://jwxk.hrbeu.edu.cn/xsxk/elective/clazz/list";
        public static readonly Dictionary<string, object> listData = new()
        {
            { "SFCT", "0" },
            //{ "XGXKLB",xgxklb["F"] }, //global filter
            //{ "KEY","网络" },
            { "campus", "01" },
            { "orderBy", "" },
            { "pageNumber",1 },
            { "pageSize" , 300 },
            { "teachingClassType" , "XGKC" }
        };

        public static async Task<HttpResponseMessage> GetRowList(this Entity entity)
        {
            var content = JsonContent.Create(listData);
            content.Headers.ContentType = new("application/json")
            {
                CharSet = "UTF-8"
            };
            content.Headers.ContentLength = content.ReadAsStringAsync().Result.Length;
            HttpRequestMessage hrt = BuildPostRequest(listUrl, entity, null, content);
            var responsePublicList = await entity.client.LimitSendAsync(hrt, entity);
            return responsePublicList;
        }

        static readonly string addUrl = "https://jwxk.hrbeu.edu.cn/xsxk/elective/clazz/add";

        public static async Task<HttpResponseMessage> Add(this Entity entity, Row @class)
        {
            var addData = new Dictionary<string, string>
            {
                { "clazzType", "XGKC" },
                { "clazzId",@class.JXBID },
                { "secretVal",@class.secretVal },
                //{ "chooseVolunteer", "1" }  //正选不传
            };
            HttpRequestMessage hrt = BuildPostRequest(addUrl, entity, new("application/x-www-form-urlencoded"), new FormUrlEncodedContent(addData));
            var addResponse = await entity.client.LimitSendAsync(hrt, entity);
            return addResponse;
        }

        static readonly string selectUrl = "https://jwxk.hrbeu.edu.cn/xsxk/elective/hrbeu/select";
        static readonly Dictionary<string, string> selectData = new()
        {
            { "jxblx","YXKCYX_XGKC"}
        };

        public static async Task<HttpResponseMessage> ValidateClaim(this Entity entity, Row @class)
        {
            HttpRequestMessage hrt = BuildPostRequest(selectUrl, entity, new("application/x-www-form-urlencoded"), new FormUrlEncodedContent(selectData));
            var selectResponse = await entity.client.LimitSendAsync(hrt, entity);
            return selectResponse;
        }



    }
}
