using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CourseClaimer.Wisedu.Shared.Dto;
using CourseClaimer.Wisedu.Shared.Models.JWXK;
using CourseClaimer.Wisedu.Shared.Models.Runtime;

namespace CourseClaimer.Wisedu.Shared.Extensions
{
    public static class EntityExtensions
    {
        public static int LimitListMillSeconds { get; set; } = 400;
        public static int LimitAddMillSeconds { get; set; } = 350;

        public static HttpRequestMessage BuildPostRequest(string url, Entity entity, MediaTypeHeaderValue? contentType, HttpContent content)
        {
            HttpRequestMessage hrt = new(HttpMethod.Post, url);
            hrt.Headers.Referrer = new(Path.Combine(entity.client.BaseAddress.AbsoluteUri, $"xsxk/elective/grablessons?batchId={entity.batchId}"));
            hrt.Headers.Host = entity.client.BaseAddress.Host;
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
        }
        public static async Task<HttpResponseMessage> LimitSendAsync(this HttpClient client, HttpRequestMessage hrm, Entity entity, bool IsAdd = false)
        {
            await DelayTillLimit(entity.stopwatch, IsAdd ? LimitAddMillSeconds : LimitListMillSeconds);
            entity.stopwatch.Restart();
            var res = await client.SendAsync(hrm);
            return res;
        }
        public static async Task<HttpResponseMessage> Captcha(this Entity entity)
        {
            var content = new FormUrlEncodedContent([]);
            content.Headers.ContentLength = 0;
            return await entity.client.PostAsync("xsxk/auth/captcha", content);
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
            var res = entity.client.PostAsync("xsxk/auth/login", content);
            return await res;
        }

        private static readonly Dictionary<string, int> xgxklb = new() //reserved
            { { "A", 12 }, { "B", 13 }, { "C", 14 }, { "D", 15 }, { "E", 16 }, { "F", 17 }, { "A0", 18 } };
        public static readonly string listUrl = "xsxk/elective/clazz/list";
        public static readonly Dictionary<string, object> listData = new()
        {
            { "SFCT", "0" },
            //{ "XGXKLB",xgxklb["F"] },
            //{ "KEY","网络" },
            { "campus", "01" },
            { "orderBy", "" },
            { "pageNumber",1 },
            { "pageSize" , 450 },
            { "teachingClassType" , "XGKC" }
            //{ "teachingClassType" , "TJKC" }
        };

        public static readonly Dictionary<string, object> listDataAll = new()
        {
            { "campus", "01" },
            { "orderBy", "" },
            { "pageNumber",1 },
            { "pageSize" , 450 },
            { "teachingClassType" , "XGKC" }
        };

        public static async Task<HttpResponseMessage> GetRowList(this Entity entity, bool SFCT = false)
        {
            while (entity.IsAddPending)
            {
                await Task.Delay(LimitListMillSeconds);
            }
            var content = JsonContent.Create(SFCT? listDataAll : listData);
            content.Headers.ContentType = new("application/json")
            {
                CharSet = "UTF-8"
            };
            //content.Headers.ContentLength = content.ReadAsStringAsync().Result.Length;
            HttpRequestMessage hrt = BuildPostRequest(listUrl, entity, null, content);
            var responsePublicList = await entity.client.LimitSendAsync(hrt, entity);
            return responsePublicList;
        }

        static readonly string addUrl = "xsxk/elective/hrbeu/add";

        public static async Task<HttpResponseMessage> Add(this Entity entity, Row @class)
        {
            var secret = entity.Secrets.FirstOrDefault(s => s.JXBID == @class.JXBID);
            if (secret == null) throw new Exception("Secret not found");
            var addData = new Dictionary<string, string>
            {
                { "clazzType", "XGKC" },
                { "clazzId",secret.classId },
                { "secretVal",secret.secretVal },
                //{ "chooseVolunteer", "1" }  //正选不传
            };
            HttpRequestMessage hrt = BuildPostRequest(addUrl, entity, new("application/x-www-form-urlencoded"), new FormUrlEncodedContent(addData));
            var addResponse = await entity.client.LimitSendAsync(hrt, entity, true);
            return addResponse;
        }

        public static async Task<HttpResponseMessage> Add(this Entity entity, RowSecretDto @class)
        {
            var secret = entity.Secrets.FirstOrDefault(s => s.JXBID == @class.JXBID);
            if (secret == null) throw new Exception("Secret not found");
            var addData = new Dictionary<string, string>
            {
                { "clazzType", "XGKC" },
                { "clazzId",secret.classId },
                { "secretVal",secret.secretVal },
                //{ "chooseVolunteer", "1" }  //正选不传
            };
            HttpRequestMessage hrt = BuildPostRequest(addUrl, entity, new("application/x-www-form-urlencoded"), new FormUrlEncodedContent(addData));
            var addResponse = await entity.client.LimitSendAsync(hrt, entity, true);
            return addResponse;
        }

        static readonly string selectUrl = "xsxk/elective/hrbeu/select";
        static readonly Dictionary<string, string> selectData = new()
        {
            { "jxblx","YXKCYX_XGKC"}
        };

        public static async Task<HttpResponseMessage> ValidateClaim(this Entity entity)
        {
            HttpRequestMessage hrt = BuildPostRequest(selectUrl, entity, new("application/x-www-form-urlencoded"), new FormUrlEncodedContent(selectData));
            var selectResponse = await entity.client.LimitSendAsync(hrt, entity, true);
            return selectResponse;
        }



    }
}
