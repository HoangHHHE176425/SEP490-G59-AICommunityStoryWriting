using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.DTOs.Account
{
    public class UpdateProfileRequest
    {
        public string? Nickname { get; set; }
        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
