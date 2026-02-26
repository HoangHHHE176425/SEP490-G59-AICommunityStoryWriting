namespace Services.DTOs.Admin.Users
{
    public class AdminUserStatsDto
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Inactive { get; set; }
        public int Banned { get; set; }
        public int Pending { get; set; }
        public int Authors { get; set; }
        public int Moderators { get; set; }
    }
}

