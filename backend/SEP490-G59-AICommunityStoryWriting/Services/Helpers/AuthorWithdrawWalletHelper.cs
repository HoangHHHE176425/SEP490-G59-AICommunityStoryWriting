using BusinessObjects.Entities;

namespace Services.Helpers
{
    /// <summary>
    /// Ledger helpers for author withdraw + PayOS payout settlement.
    /// On create-withdraw we move coins income_balance -&gt; frozen_balance.
    /// On payout COMPLETED we release frozen; if frozen is short (data drift), take the remainder from income_balance.
    /// </summary>
    public static class AuthorWithdrawWalletHelper
    {
        /// <summary>
        /// Maps PayOS payout approval / first transaction state to local terminal COMPLETED/FAILED, or null if still in progress.
        /// </summary>
        public static string? MapPayOSPayoutToSettlementStatus(string? approvalState, string? firstTransactionState)
        {
            var txState = (firstTransactionState ?? string.Empty).Trim().ToUpperInvariant();
            if (txState is "SUCCEEDED" or "COMPLETED") return "COMPLETED";
            if (txState is "FAILED" or "CANCELLED" or "REVERSED") return "FAILED";

            var approval = (approvalState ?? string.Empty).Trim().ToUpperInvariant();
            if (approval is "SUCCEEDED" or "COMPLETED" or "PARTIAL_COMPLETED") return "COMPLETED";
            if (approval is "FAILED" or "REJECTED" or "CANCELLED") return "FAILED";
            return null;
        }

        /// <summary>
        /// Finalize a successful payout: remove <paramref name="amountCoins"/> from frozen first, then from income if frozen was insufficient.
        /// </summary>
        public static void ApplyPayoutCompleted(wallets wallet, decimal amountCoins)
        {
            if (wallet == null || amountCoins <= 0m) return;

            var frozen = wallet.frozen_balance ?? 0m;
            var takeFromFrozen = Math.Min(frozen, amountCoins);
            wallet.frozen_balance = frozen - takeFromFrozen;

            var remainder = amountCoins - takeFromFrozen;
            if (remainder > 0m)
            {
                var income = wallet.income_balance ?? 0m;
                wallet.income_balance = Math.Max(0m, income - remainder);
            }
        }
    }
}
