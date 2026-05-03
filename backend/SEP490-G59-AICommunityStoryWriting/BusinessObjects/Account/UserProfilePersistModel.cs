namespace BusinessObjects.Account;

/// <summary>Field-level flags for saving account profile (avoids reusing a tracked user graph).</summary>
public sealed class UserProfilePersistModel
{
    public bool SetNickname { get; init; }
    public string? Nickname { get; init; }

    public bool SetPhone { get; init; }
    public string? Phone { get; init; }

    public bool SetIdNumber { get; init; }
    public string? IdNumber { get; init; }

    public bool SetBio { get; init; }
    public string? Bio { get; init; }

    public bool SetDescription { get; init; }
    public string? Description { get; init; }

    public bool SetAvatarUrl { get; init; }
    public string? AvatarUrl { get; init; }
}
