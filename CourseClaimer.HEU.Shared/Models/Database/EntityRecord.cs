using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourseClaimer.HEU.Shared.Models.Database
{
    public class EntityRecord
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Time { get; set; } = DateTime.Now;
        public string UserName { get; set; }
        public string Message { get; set; }
    }
}
