using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.DTOs.Account
{
    public class ChangePasswordRequest
    {
        //public int UserId { get; set; } 

        [Required]
        public string OldPassword { get; set; } = null!;

        [Required, MinLength(6)]
        public string NewPassword { get; set; } = null!;
    }
}
