using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourseClaimer.HEU.Shared.Models.Database
{
    public class ClaimRecord
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserName { get; set; }
        public DateTime ClaimTime { get; set; } = DateTime.Now;
        public string Course { get; set; }
        public bool IsSuccess { get; set; }
    }
}
