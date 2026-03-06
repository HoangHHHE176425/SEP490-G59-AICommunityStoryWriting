using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

/// <summary>Reaction của user cho comment (story). Mỗi user chỉ 1 reaction/comment; đổi type = đổi reaction.</summary>
public partial class comment_reactions
{
    public Guid user_id { get; set; }

    public Guid comment_id { get; set; }

    /// <summary>LIKE, DISLIKE, FUNNY, SAD, ANGRY, LOVE, WOW</summary>
    public string? reaction_type { get; set; }

    public DateTime? created_at { get; set; }

    public virtual comments comment { get; set; } = null!;

    public virtual users user { get; set; } = null!;

    /// <summary>Các loại reaction hỗ trợ cho comment story.</summary>
    public static class ReactionTypes
    {
        public const string Like = "LIKE";
        public const string Dislike = "DISLIKE";
        public const string Funny = "FUNNY";
        public const string Sad = "SAD";
        public const string Angry = "ANGRY";
        public const string Love = "LOVE";
        public const string Wow = "WOW";

        public static readonly IReadOnlyList<string> All = new[] { Like, Dislike, Funny, Sad, Angry, Love, Wow };

        public static bool IsValid(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return false;
            return All.Contains(type.Trim().ToUpperInvariant());
        }
    }
}
