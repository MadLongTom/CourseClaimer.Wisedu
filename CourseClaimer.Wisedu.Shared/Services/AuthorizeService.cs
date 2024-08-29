using System.Net;
using System.Security.Cryptography;
using System.Text;
using CourseClaimer.Ocr;
using CourseClaimer.Wisedu.Shared.Enums;
using CourseClaimer.Wisedu.Shared.Extensions;
using CourseClaimer.Wisedu.Shared.Models.Database;
using CourseClaimer.Wisedu.Shared.Models.JWXK.Roots;
using CourseClaimer.Wisedu.Shared.Models.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CourseClaimer.Wisedu.Shared.Services
{
    public class AuthorizeService(Aes aesUtil, OcrService ocr, IHttpClientFactory clientFactory,IConfiguration configuration,IServiceProvider serviceProvider)
    {
        public HttpClient BuildClient()
        {
            var client = new HttpClient();
            client.BaseAddress = new(configuration["BasePath"]);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new("text/plain"));
            client.DefaultRequestHeaders.Accept.Add(new("*/*"));
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0");
            client.DefaultRequestHeaders.Connection.Add("keep-alive");
            return client;
        }
        public async Task<LoginResult> MakeUserLogin(Entity entity,bool IsReLogin = false)
        {
            var dbContext = serviceProvider.GetRequiredService<ClaimDbContext>();
            dbContext.EntityRecords.Add(new EntityRecord()
            {
                UserName = entity.username,
                Message = $"MakeUserLogin: {entity.username} entered with IsReLogin={IsReLogin}"
            });
            await dbContext.SaveChangesAsync();
            if (IsReLogin) await Task.Delay(Convert.ToInt32(configuration["ReLoginDelayMilliseconds"]));
            entity.finished = false;
            entity.client = clientFactory.CreateClient("JWXK");
            //entity.client = BuildClient();
            var captcha = await entity.Captcha().ToResponseDto<CaptchaRoot>();
            captcha.EnsureSuccess();
            var authCode = ocr.classification(img_base64: captcha!.Data.data.captcha.Split(',')[1]);
            var login = await entity.Login(AESEncrypt(entity.password), captcha.Data.data.uuid, authCode).ToResponseDto<LoginRoot>();
            if (login.InnerMessage.Contains("密码错误")) return LoginResult.WrongPassword;
            if (login.InnerMessage.Contains("验证码")) return LoginResult.WrongCaptcha;
            switch (login.InnerCode)
            {
                case HttpStatusCode.InternalServerError:
                    return LoginResult.UnknownError;
                case HttpStatusCode.OK:
                    entity.batchId = login.Data.data.student.hrbeuLcMap.First().Key;
                    entity.client.DefaultRequestHeaders.Authorization = new(login.Data.data.token);
                    entity.client.DefaultRequestHeaders.Add("Cookie", $"Authorization={login.Data.data.token}");
                    entity.client.DefaultRequestHeaders.Add("batchId", entity.batchId);
                    return LoginResult.Success;
                default:
                    login.EnsureSuccess();
                    return LoginResult.UnknownError;
            }
        }

        private string AESEncrypt(string text)
        {
            var cipher = aesUtil.EncryptEcb(Encoding.UTF8.GetBytes(text), PaddingMode.PKCS7);
            return Convert.ToBase64String(cipher);
        }
    }
}
