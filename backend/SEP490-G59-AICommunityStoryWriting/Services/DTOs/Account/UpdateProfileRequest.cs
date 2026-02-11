using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.DTOs.Account
{
    public class UpdateProfileRequest
    {
        public string? DisplayName { get; set; }

        public string? Phone { get; set; }
        public string? IdNumber { get; set; }
        public string? Bio { get; set; }
        public string? Description { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
