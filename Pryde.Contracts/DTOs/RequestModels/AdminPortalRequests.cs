using Pryde.Domain.Enums;

namespace Pryde.Contracts.RequestModels;

public class InviteStaffRequestDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class AdminStaffRequestDto : PaginationRequestDto
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public UserStatus? Status { get; set; }
}

public class AdminDriversRequestDto : PaginationRequestDto
{
    public string? Search { get; set; }
    public UserStatus? Status { get; set; }
    public KycStatus? KycStatus { get; set; }
    public VehicleDocumentReviewStatus? DocumentStatus { get; set; }
}

public class RejectionRequestDto
{
    public string Reason { get; set; } = string.Empty;
}

public class BookingPaymentRequestDto
{
    public string IdempotencyKey { get; set; } = string.Empty;
}

public class AdminWalletTransactionsRequestDto : PaginationRequestDto
{
    public Guid? UserId { get; set; }
    public WalletTransactionType? TransactionType { get; set; }
    public string? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Reference { get; set; }
    public string? Search { get; set; }
}

public class AdminEscrowsRequestDto : PaginationRequestDto
{
    public EscrowStatus? Status { get; set; }
    public Guid? BookingId { get; set; }
    public Guid? PassengerId { get; set; }
    public Guid? DriverId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}

public class AdminLedgerTransactionsRequestDto : PaginationRequestDto
{
    public LedgerTransactionType? TransactionType { get; set; }
    public LedgerTransactionStatus? Status { get; set; }
    public string? Reference { get; set; }
    public Guid? BookingId { get; set; }
    public Guid? EscrowId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}
