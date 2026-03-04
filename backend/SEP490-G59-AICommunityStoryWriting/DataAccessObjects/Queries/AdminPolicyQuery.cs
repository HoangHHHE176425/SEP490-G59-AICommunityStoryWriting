namespace DataAccessObjects.Queries
{
    public class AdminPolicyQuery
    {
        public string? Type { get; set; }
        public bool? IsActive { get; set; }
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SortBy { get; set; } = "created_at";
        public string? SortOrder { get; set; } = "desc";
    }
}

