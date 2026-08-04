namespace Pryde.Domain.Enums;

public enum NotificationType
{
    BookingRequested = 1,
    BookingApproved = 2,
    BookingDeclined = 3,
    BookingCancelled = 4,
    BookingPaymentRequired = 5,
    BookingPaymentSuccessful = 6,
    BookingPaymentExpired = 7,
    TripStartingSoon = 8,
    PickupConfirmationRequired = 9,
    DropoffConfirmationRequired = 10,
    TripCompleted = 11,
    EscrowReleased = 12,
    WalletCredited = 13,
    WithdrawalSubmitted = 14,
    WithdrawalSuccessful = 15,
    WithdrawalFailed = 16,
    KycApproved = 17,
    KycRejected = 18,
    DriverApproved = 19,
    DriverRejected = 20,
    VehicleApproved = 21,
    VehicleRejected = 22,
    SystemAnnouncement = 23,
    DriverDeactivated = 24,
    RatingReceived = 25
}
