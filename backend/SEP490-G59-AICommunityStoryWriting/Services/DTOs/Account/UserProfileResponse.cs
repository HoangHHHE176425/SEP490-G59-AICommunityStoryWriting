using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.DTOs.Account
{
    public class UserProfileResponse
    {
        public Guid Id { get; set; }

        public string DisplayName { get; set; } = "User";

        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public string? IdNumber { get; set; }
        public string? Description { get; set; }
        public string? JoinDate { get; set; }   
        public bool IsVerified { get; set; }    
        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; }

        /// <summary>Role từ DB: USER, AUTHOR, MODERATOR, ADMIN. Dùng cho FE kiểm tra quyền admin.</summary>
        public string? Role { get; set; }

        public List<string> Tags { get; set; } = new List<string>();

        public UserStats Stats { get; set; } = new UserStats();
    }

    public class UserStats
    {
        public int StoriesWritten { get; set; }
        public long TotalReads { get; set; }
        public int Likes { get; set; }
        public int CurrentCoins { get; set; } // Trả về 0
    }
}
