using System.Collections.Generic;

namespace Services.DTOs.Admin.Policies
{
    public class AdminPolicyStatsDto
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public Dictionary<string, int> ByType { get; set; } = new();
    }
}

