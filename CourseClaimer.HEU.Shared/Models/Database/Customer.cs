using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourseClaimer.HEU.Shared.Models.Database
{
    public class Customer
    {
        [Key] public Guid Id { get; set; } = Guid.NewGuid();
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Categories { get; set; } = string.Empty;
        public string Course { get; set; } = string.Empty;
        public bool IsFinished { get; set; }
    }
}
