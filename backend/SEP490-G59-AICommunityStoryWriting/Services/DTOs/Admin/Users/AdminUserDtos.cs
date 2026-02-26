using System.ComponentModel.DataAnnotations;

namespace Services.DTOs.Admin.Users
{
    public class AdminUserQueryDto
    {
        public string? Search { get; set; }
        public string? Role { get; set; }
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SortBy { get; set; } = "created_at";
        public string? SortOrder { get; set; } = "desc";
    }

    public class AdminUserListItemDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string? Role { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }

        public string? Nickname { get; set; }
        public string? Phone { get; set; }
        public string? IdNumber { get; set; }
        public bool IsEmailVerified { get; set; }
    }

    public class AdminUserDetailDto : AdminUserListItemDto
    {
        public DateTime? UpdatedAt { get; set; }
        public DateTime? EmailVerifiedAt { get; set; }
        public DateTime? DeletionRequestedAt { get; set; }
    }

    public class AdminCreateUserRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Required, MinLength(6)]
        public string Password { get; set; } = null!;

        public string Role { get; set; } = "USER";
        public string Status { get; set; } = "ACTIVE";

        public string? Nickname { get; set; }
        public string? Phone { get; set; }
        public string? IdNumber { get; set; }
    }

    public class AdminSetUserRoleRequest
    {
        [Required]
        public string Role { get; set; } = null!;
    }

    public class AdminSetUserStatusRequest
    {
        [Required]
        public string Status { get; set; } = null!;
    }
}

