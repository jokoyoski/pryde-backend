using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public sealed class OnboardingStatusService(IUnitOfWork unitOfWork)
    : IOnboardingStatusService
{
    public async Task<OnboardingStatusResponseDto> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        if (!user.IsEmailVerified)
        {
            throw new ForbiddenException(
                "Email verification is required before login.");
        }

        var userRoles = await unitOfWork.UserRoles.GetByUserIdAsync(
            userId,
            cancellationToken);
        var roles = userRoles
            .Select(userRole => userRole.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(RoleOrder)
            .ThenBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasDriverRole = roles.Contains(
            RoleNames.Driver,
            StringComparer.OrdinalIgnoreCase);
        var hasPassengerRole = roles.Contains(
            RoleNames.Passenger,
            StringComparer.OrdinalIgnoreCase);
        var isStaff = roles.Any(role =>
            role is RoleNames.Admin or RoleNames.SuperAdmin);

        if (!hasDriverRole && !hasPassengerRole)
        {
            return isStaff
                ? CompletedStaffResponse(roles)
                : BuildResponse(
                    roles,
                    OnboardingStage.RoleSelection,
                    [],
                    null,
                    null,
                    null,
                    false,
                    false);
        }

        var completedStages = new List<OnboardingStage>
        {
            OnboardingStage.RoleSelection
        };
        var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(
            userId,
            cancellationToken);

        if (kyc?.Status != KycStatus.Approved)
        {
            return BuildResponse(
                roles,
                OnboardingStage.IdentityVerification,
                completedStages,
                kyc?.Status,
                null,
                kyc?.Status == KycStatus.Rejected
                    ? kyc.RejectionReason
                    : null,
                false,
                false);
        }

        completedStages.Add(OnboardingStage.IdentityVerification);

        if (!hasDriverRole)
        {
            return BuildResponse(
                roles,
                OnboardingStage.Completed,
                completedStages,
                kyc.Status,
                null,
                null,
                true,
                false);
        }

        var vehicles = await unitOfWork.Vehicles.GetByUserIdAsync(
            userId,
            cancellationToken);
        var vehicle = SelectDriverOnboardingVehicle(vehicles);
        var verificationStatus = MapDriverVerificationStatus(vehicle);

        if (vehicle is null)
        {
            return BuildResponse(
                roles,
                OnboardingStage.DriverDocuments,
                completedStages,
                kyc.Status,
                verificationStatus,
                null,
                false,
                false);
        }

        var documents = await unitOfWork.VehicleDocuments.GetByVehicleIdAsync(
            vehicle.Id,
            cancellationToken);
        var driverDocumentsCompleted =
            vehicle.OnboardingStatus != VehicleOnboardingStatus.Draft ||
            documents.Any(document =>
                document.DocumentType ==
                VehicleDocumentType.VehicleRegistration);

        if (!driverDocumentsCompleted)
        {
            return BuildResponse(
                roles,
                OnboardingStage.DriverDocuments,
                completedStages,
                kyc.Status,
                verificationStatus,
                null,
                false,
                false);
        }

        completedStages.Add(OnboardingStage.DriverDocuments);

        if (vehicle.OnboardingStatus == VehicleOnboardingStatus.Draft)
        {
            return BuildResponse(
                roles,
                OnboardingStage.VehicleInformation,
                completedStages,
                kyc.Status,
                verificationStatus,
                null,
                false,
                false);
        }

        completedStages.Add(OnboardingStage.VehicleInformation);

        if (vehicle.OnboardingStatus is
            VehicleOnboardingStatus.PendingReview or
            VehicleOnboardingStatus.Rejected)
        {
            return BuildResponse(
                roles,
                OnboardingStage.SubmittedForReview,
                completedStages,
                kyc.Status,
                verificationStatus,
                vehicle.OnboardingStatus == VehicleOnboardingStatus.Rejected
                    ? vehicle.RejectionReason
                    : null,
                true,
                false);
        }

        completedStages.Add(OnboardingStage.SubmittedForReview);
        return BuildResponse(
            roles,
            OnboardingStage.Completed,
            completedStages,
            kyc.Status,
            verificationStatus,
            null,
            true,
            vehicle.IsActive);
    }

    private static OnboardingStatusResponseDto CompletedStaffResponse(
        IReadOnlyList<string> roles) =>
        BuildResponse(
            roles,
            OnboardingStage.Completed,
            [],
            null,
            null,
            null,
            true,
            false);

    private static OnboardingStatusResponseDto BuildResponse(
        IReadOnlyList<string> roles,
        OnboardingStage currentStage,
        IReadOnlyList<OnboardingStage> completedStages,
        KycStatus? kycStatus,
        DriverVerificationStatus? driverVerificationStatus,
        string? rejectionReason,
        bool onboardingCompleted,
        bool driverAccessGranted) =>
        new()
        {
            Roles = roles,
            CurrentStage = currentStage,
            CompletedStages = completedStages,
            NextStage = currentStage is
                OnboardingStage.SubmittedForReview or
                OnboardingStage.Completed
                ? null
                : currentStage,
            KycStatus = kycStatus,
            DriverVerificationStatus = driverVerificationStatus,
            RejectionReason = rejectionReason,
            OnboardingCompleted = onboardingCompleted,
            DriverAccessGranted = driverAccessGranted
        };

    private static Vehicle? SelectDriverOnboardingVehicle(
        IEnumerable<Vehicle> vehicles) =>
        vehicles
            .OrderByDescending(vehicle => VehicleStatusOrder(
                vehicle.OnboardingStatus))
            .ThenByDescending(vehicle => vehicle.CreatedAt)
            .FirstOrDefault();

    private static DriverVerificationStatus MapDriverVerificationStatus(
        Vehicle? vehicle) =>
        vehicle?.OnboardingStatus switch
        {
            VehicleOnboardingStatus.PendingReview =>
                DriverVerificationStatus.Pending,
            VehicleOnboardingStatus.Approved =>
                DriverVerificationStatus.Approved,
            VehicleOnboardingStatus.Rejected =>
                DriverVerificationStatus.ResubmissionRequired,
            _ => DriverVerificationStatus.NotSubmitted
        };

    private static int VehicleStatusOrder(
        VehicleOnboardingStatus status) =>
        status switch
        {
            VehicleOnboardingStatus.Approved => 4,
            VehicleOnboardingStatus.PendingReview => 3,
            VehicleOnboardingStatus.Rejected => 2,
            _ => 1
        };

    private static int RoleOrder(string role) =>
        role switch
        {
            RoleNames.Passenger => 1,
            RoleNames.Driver => 2,
            RoleNames.Admin => 3,
            RoleNames.SuperAdmin => 4,
            _ => 5
        };
}
