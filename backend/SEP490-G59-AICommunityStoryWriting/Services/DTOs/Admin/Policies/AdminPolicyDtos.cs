using System.ComponentModel.DataAnnotations;

namespace Services.DTOs.Admin.Policies
{
    public class AdminPolicyQueryDto
    {
        public string? Type { get; set; }
        public bool? IsActive { get; set; }
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SortBy { get; set; } = "created_at";
        public string? SortOrder { get; set; } = "desc";
    }

    public class AdminPolicyListItemDto
    {
        public Guid Id { get; set; }
        public string? Type { get; set; }
        public string Version { get; set; } = null!;
        public bool IsActive { get; set; }
        public bool RequireResign { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ActivatedAt { get; set; }
    }

    public class AdminPolicyDetailDto : AdminPolicyListItemDto
    {
        public string Content { get; set; } = null!;
    }

    public class AdminCreatePolicyRequest
    {
        [Required]
        public string Type { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string Version { get; set; } = null!;

        [Required]
        public string Content { get; set; } = null!;

        public bool IsActive { get; set; } = false;

        public bool RequireResign { get; set; } = false;
    }

    public class AdminUpdatePolicyRequest
    {
        [Required]
        public string Type { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string Version { get; set; } = null!;

        [Required]
        public string Content { get; set; } = null!;

        public bool IsActive { get; set; }

        public bool RequireResign { get; set; }
    }
}

