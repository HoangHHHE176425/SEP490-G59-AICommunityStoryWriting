using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Donation
{
    public Guid Id { get; set; }

    public Guid? SenderId { get; set; }

    public Guid? ReceiverId { get; set; }

    public Guid? StoryId { get; set; }

    public int Amount { get; set; }

    public string? Message { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? Receiver { get; set; }

    public virtual User? Sender { get; set; }
}
