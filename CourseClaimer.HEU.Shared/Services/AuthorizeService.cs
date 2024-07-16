using CourseClaimer.HEU.Shared.Enums;
using CourseClaimer.HEU.Shared.Extensions;
using CourseClaimer.Ocr;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace CourseClaimer.HEU.Shared.Services
{
    public class AuthorizeService(Aes aesUtil, OcrService ocr, IHttpClientFactory clientFactory)
    {
        public async Task<LoginResult> MakeUserLogin(Entity entity)
        {
            entity.client = clientFactory.CreateClient("JWXK");
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
