using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourseClaimer.HEU.Shared.Enums
{
    public enum LoginResult
    {
        Success = 0,
        WrongPassword = 1,
        WrongCaptcha = 2,
        UnknownError = 3
    }
}
