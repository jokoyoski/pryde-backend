namespace Pryde.Domain.Enums;

public enum LedgerTransactionType
{
    BookingPaymentHold = 1,
    EscrowRelease = 2,
    EscrowRefund = 3,
    DriverWithdrawal = 4,
    TestWalletFunding = 5
}
