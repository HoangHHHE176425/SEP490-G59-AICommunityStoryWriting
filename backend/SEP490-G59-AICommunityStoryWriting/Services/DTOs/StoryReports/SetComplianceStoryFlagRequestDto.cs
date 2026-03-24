namespace Services.DTOs.StoryReports;

public class SetComplianceStoryFlagRequestDto
{
    public bool Flagged { get; set; }
    public string? Note { get; set; }
}

public class SetComplianceStoryBoolRequestDto
{
    public bool Value { get; set; }
}
