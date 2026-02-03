using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class coin_orders
{
    public Guid id { get; set; }

    public Guid? user_id { get; set; }

    public int? package_id { get; set; }

    public decimal amount_paid { get; set; }

    public int coins_granted { get; set; }

    public string? payment_gateway { get; set; }

    public string? gateway_transaction_id { get; set; }

    public string? gateway_response_code { get; set; }

    public string? status { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? completed_at { get; set; }

    public virtual coin_packages? package { get; set; }

    public virtual users? user { get; set; }
}
