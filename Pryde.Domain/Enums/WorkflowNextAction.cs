namespace Pryde.Domain.Enums;

public enum WorkflowNextAction
{
    None = 0,
    VerifyEmail = 1,
    Login = 2,
    SelectRole = 3,
    CompleteKyc = 4,
    CompleteVehicleOnboarding = 5,
    AwaitAdminApproval = 6,
    CreateTrip = 7,
    AwaitDriverDecision = 8,
    PayForBooking = 9,
    DriverStartTrip = 10,
    PassengerConfirmPickup = 11,
    DriverEndTrip = 12,
    PassengerConfirmDropoff = 13,
    SubmitReview = 14,
    RequestWithdrawalOtp = 15,
    SubmitWithdrawal = 16
}

public enum WorkflowActor
{
    None = 0,
    User = 1,
    Driver = 2,
    Passenger = 3,
    Admin = 4
}

public enum WorkflowOperationStatus
{
    Accepted = 1,
    Completed = 2
}
