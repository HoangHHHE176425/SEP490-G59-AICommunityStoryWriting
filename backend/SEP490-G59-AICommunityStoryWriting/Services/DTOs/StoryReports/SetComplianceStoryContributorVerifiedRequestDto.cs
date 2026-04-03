using System;
using System.Collections.Generic;

namespace Services.DTOs.StoryReports;

public class SetComplianceStoryContributorVerifiedRequestDto
{
    public IReadOnlyList<Guid>? VerifyUserIds { get; set; }
    public IReadOnlyList<Guid>? UnverifyUserIds { get; set; }
}
