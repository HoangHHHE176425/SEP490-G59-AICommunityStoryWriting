using System;
using System.Collections.Generic;

namespace Services.DTOs.CommentReports;

public class SetComplianceCommentReportEvidenceVerifiedRequestDto
{
    public IReadOnlyList<Guid>? VerifyEvidenceIds { get; set; }
    public IReadOnlyList<Guid>? UnverifyEvidenceIds { get; set; }
}
