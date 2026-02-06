using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class AuthorBankAccount
{
    public Guid UserId { get; set; }

    public string BankName { get; set; } = null!;

    public string AccountNumber { get; set; } = null!;

    public string AccountHolderName { get; set; } = null!;

    public string? BranchName { get; set; }

    public bool? IsVerified { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
