using Microsoft.Extensions.Logging;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Notifications;
using Pryde.Services.Notifications.Interface;
using Pryde.Services.Providers.Dojah;
using Pryde.Services.Security.Interface;
using Pryde.Services.Service.Interface;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace Pryde.Services.Service.Implementation;

public class AdminPortalService(
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IEmailService emailService,
    IFinancialService financialService,
    IDojahApiClient dojahApiClient,
    ILogger<AdminPortalService> logger,
    INotificationService notificationService) : IAdminPortalService
{
    public AdminPortalService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        IFinancialService financialService,
        IDojahApiClient dojahApiClient,
        ILogger<AdminPortalService> logger)
        : this(
            unitOfWork,
            passwordHasher,
            emailService,
            financialService,
            dojahApiClient,
            logger,
            new NotificationService(unitOfWork))
    {
    }

    public async Task<StaffResponseDto> InviteStaffAsync(
        InviteStaffRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateInvitation(request);
        var email = request.Email.Trim().ToLowerInvariant();
        var roleName = NormalizeStaffRole(request.Role);
        var existing = await unitOfWork.Users.GetByEmailAsync(email, cancellationToken);
        User user;

        if (existing is not null)
        {
            var existingRoles = await unitOfWork.UserRoles.GetByUserIdAsync(existing.Id, cancellationToken);
            if (!existingRoles.Any(role => IsStaffRole(role.Role.Name)))
                throw new ConflictException("This email is already used by a non-staff account.");
            if (existing.Status == UserStatus.Active)
                throw new ConflictException("An active staff account already uses this email.");
            if (existing.Status != UserStatus.Pending)
                throw new ConflictException("This staff account already exists and must be reactivated instead.");
            if (!existingRoles.Any(userRole => userRole.Role.Name == roleName))
                throw new ConflictException("The pending invitation already uses a different staff role.");

            var activeCode = await unitOfWork.PasswordResetCodes.GetLatestActiveByUserIdAsync(existing.Id, cancellationToken);
            if (activeCode?.IsValid == true)
                throw new ConflictException("An unexpired invitation already exists for this email.");

            user = existing;
            await unitOfWork.PasswordResetCodes.InvalidateAllForUserAsync(user.Id, cancellationToken);
        }
        else
        {
            user = new User
            {
                Email = email,
                PhoneNumber = $"STAFF{Guid.NewGuid():N}"[..20],
                PasswordHash = passwordHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
                Status = UserStatus.Pending
            };
            await unitOfWork.Users.CreateAsync(user, cancellationToken);
            await unitOfWork.Profiles.CreateAsync(new Profile
            {
                UserId = user.Id,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim()
            }, cancellationToken);

            var role = await unitOfWork.Roles.GetByNameAsync(roleName, cancellationToken)
                ?? throw new NotFoundException(nameof(Role), roleName);
            await unitOfWork.UserRoles.CreateAsync(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            }, cancellationToken);
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        await unitOfWork.PasswordResetCodes.CreateAsync(new PasswordResetCode
        {
            UserId = user.Id,
            CodeHash = HashCode(code),
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await SendStaffInvitationEmailAsync(email, request.FirstName, roleName, code, cancellationToken);

        return await GetStaffByIdAsync(user.Id, cancellationToken);
    }
    private Task SendStaffInvitationEmailAsync( string email, string? firstName, string roleName,string code,
     CancellationToken cancellationToken)
    {
        var safeFirstName = string.IsNullOrWhiteSpace(firstName)
            ? null
            : HtmlEncoder.Default.Encode(firstName.Trim());

        var safeRoleName = HtmlEncoder.Default.Encode(roleName);
        var safeCode = HtmlEncoder.Default.Encode(code);

        var greeting = safeFirstName is null
            ? "Hello,"
            : $"Hello {safeFirstName},";

        var emailBody = $"""
        <div style="margin:0; padding:24px; background-color:#f5f7fa; font-family:Arial, Helvetica, sans-serif; color:#1f2937;">
            <div style="max-width:600px; margin:0 auto; background-color:#ffffff; border:1px solid #e5e7eb; border-radius:12px; overflow:hidden;">

                <div style="padding:28px 32px; text-align:center; background-color:#111827;">
                    <h1 style="margin:0; color:#ffffff; font-size:28px;">
                        Pryde
                    </h1>
                </div>

                <div style="padding:32px;">
                    <p style="margin:0 0 20px; font-size:16px; line-height:1.6;">
                        {greeting}
                    </p>

                    <h2 style="margin:0 0 16px; font-size:22px; color:#111827;">
                        You have been invited to join Pryde
                    </h2>

                    <p style="margin:0 0 20px; font-size:16px; line-height:1.6;">
                        You have been invited to join the Pryde administration team as a
                        <strong>{safeRoleName}</strong>.
                    </p>

                    <p style="margin:0 0 24px; font-size:16px; line-height:1.6;">
                        Use the invitation code below to continue setting up your account:
                    </p>

                    <div style="margin:24px 0; padding:22px; text-align:center; background-color:#f3f4f6; border-radius:8px;">
                        <span style="font-size:32px; font-weight:700; letter-spacing:8px; color:#111827;">
                            {safeCode}
                        </span>
                    </div>

                    <p style="margin:0 0 18px; font-size:15px; line-height:1.6; color:#4b5563;">
                        This invitation code will expire in
                        <strong>24 hours</strong>.
                    </p>

                    <p style="margin:0 0 18px; font-size:15px; line-height:1.6; color:#4b5563;">
                        Use the existing password reset flow to set your password
                        and activate your account.
                    </p>

                    <p style="margin:0 0 24px; font-size:15px; line-height:1.6; color:#4b5563;">
                        If you were not expecting this invitation,
                        you can safely ignore this email.
                    </p>

                    <p style="margin:0; font-size:15px; line-height:1.6;">
                        Regards,<br />
                        <strong>The Pryde Team</strong>
                    </p>
                </div>

                <div style="padding:20px 32px; text-align:center; background-color:#f9fafb; border-top:1px solid #e5e7eb;">
                    <p style="margin:0; font-size:13px; color:#6b7280;">
                        © {DateTime.UtcNow.Year} Pryde. All rights reserved.
                    </p>
                </div>

            </div>
        </div>
        """;

        return emailService.SendAsync(
            email,
            "Your Pryde administrator invitation",
            emailBody,
            cancellationToken);
    }

    public async Task<StaffListResponseDto> GetStaffAsync(
        AdminStaffRequestDto request, CancellationToken cancellationToken = default)
    {
        request ??= new AdminStaffRequestDto();
        if (!string.IsNullOrWhiteSpace(request.Role)) NormalizeStaffRole(request.Role);
        var result = await unitOfWork.AdminListings.GetStaffAsync(
            request.Search, request.Role, request.Status, request.PageNumber, request.PageSize, cancellationToken);
        var summary = await unitOfWork.AdminListings.GetStaffSummaryAsync(cancellationToken);
        return new StaffListResponseDto
        {
            Items = result.Items.Select(MapStaff).ToList(),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = (int)Math.Ceiling(result.TotalCount / (double)request.PageSize),
            Summary = new StaffSummaryResponseDto
            {
                TotalStaff = summary.TotalStaff,
                ActiveStaff = summary.ActiveStaff,
                InactiveStaff = summary.InactiveStaff,
                PendingInvites = summary.PendingInvites
            }
        };
    }

    public async Task<StaffResponseDto> GetStaffByIdAsync(
        Guid staffId, CancellationToken cancellationToken = default)
    {
        var user = await GetStaffEntityAsync(staffId, cancellationToken);
        return MapStaff(user);
    }

    public Task<StaffResponseDto> ActivateStaffAsync(
        Guid staffId, CancellationToken cancellationToken = default) =>
        SetStaffStatusAsync(staffId, UserStatus.Active, null, cancellationToken);

    public Task<StaffResponseDto> DeactivateStaffAsync(
        Guid staffId, Guid currentUserId, CancellationToken cancellationToken = default) =>
        SetStaffStatusAsync(staffId, UserStatus.Deactivated, currentUserId, cancellationToken);

    public async Task<AdminUserDetailResponseDto> GetUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetCustomerAsync(userId, false, cancellationToken);
        return MapUserDetail(user);
    }

    public Task<AdminUserDetailResponseDto> ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        SetCustomerStatusAsync(userId, UserStatus.Active, false, cancellationToken);

    public Task<AdminUserDetailResponseDto> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        SetCustomerStatusAsync(userId, UserStatus.Deactivated, false, cancellationToken);

    public async Task<PagedResponseDto<AdminUserSummaryResponseDto>> GetDriversAsync(
        AdminDriversRequestDto request, CancellationToken cancellationToken = default)
    {
        request ??= new AdminDriversRequestDto();
        var result = await unitOfWork.AdminListings.GetDriversAsync(
            request.Search, request.Status, request.KycStatus, request.DocumentStatus,
            request.PageNumber, request.PageSize, cancellationToken);
        return new PagedResponseDto<AdminUserSummaryResponseDto>
        {
            Items = result.Items.Select(MapUserSummary).ToList(),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = (int)Math.Ceiling(result.TotalCount / (double)request.PageSize)
        };
    }

    public async Task<AdminDriverDetailResponseDto> GetDriverAsync(
        Guid driverId, CancellationToken cancellationToken = default)
    {
        var user = await GetCustomerAsync(driverId, true, cancellationToken);
        var tripSummary = await unitOfWork.AdminListings.GetDriverTripSummaryAsync(driverId, cancellationToken);
        var ratingSummary = await unitOfWork.TripRatings.GetSummaryAsync(driverId, cancellationToken);
        return MapDriverDetail(user, tripSummary, ratingSummary);
    }

    public async Task<AdminDriverDetailResponseDto> ActivateDriverAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var result = await SetDriverStatusAsync(
            driverId,
            UserStatus.Active,
            cancellationToken);
        await NotifyDriverReviewAsync(
            driverId,
            true,
            cancellationToken);
        if (result.StatusChanged)
        {
            await emailService.SendAsync(
                result.Response.Email,
                "Your Pryde driver onboarding is approved",
                PrydeEmailTemplates.DriverOnboardingApproved(
                    result.Response.FirstName),
                cancellationToken);
        }
        return result.Response;
    }

    public async Task<AdminDriverDetailResponseDto> DeactivateDriverAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var result = await SetDriverStatusAsync(
            driverId,
            UserStatus.Deactivated,
            cancellationToken);
        await NotifyDriverReviewAsync(
            driverId,
            false,
            cancellationToken);
        if (result.StatusChanged)
        {
            await emailService.SendAsync(
                result.Response.Email,
                "Your Pryde driver account was deactivated",
                PrydeEmailTemplates.DriverAccountDeactivated(
                    result.Response.FirstName),
                cancellationToken);
        }
        return result.Response;
    }

    private Task NotifyDriverReviewAsync(
        Guid driverId,
        bool approved,
        CancellationToken cancellationToken)
    {
        return notificationService.TryCreateAsync(
            new CreateNotificationRequest
            {
                UserId = driverId,
                Type = approved
                    ? NotificationType.DriverApproved
                    : NotificationType.DriverDeactivated,
                Title = approved
                    ? "Driver onboarding approved"
                    : "Driver account deactivated",
                Message = approved
                    ? "Your driver onboarding was approved."
                    : "Your driver account was deactivated.",
                RelatedEntityId = driverId,
                RelatedEntityType = "Driver",
                DeduplicationKey = approved
                    ? $"driver-approved:{driverId}"
                    : $"driver-deactivated:{driverId}"
            },
            cancellationToken);
    }

    public async Task<AdminKycResponseDto> GetKycAsync(
        Guid kycId, CancellationToken cancellationToken = default)
    {
        var kyc = await unitOfWork.AdminListings.GetKycDetailsAsync(kycId, cancellationToken)
            ?? throw new NotFoundException(nameof(KycVerification), kycId);
        var response = MapKyc(kyc);
        if (string.IsNullOrWhiteSpace(kyc.DojahReference))
        {
            logger.LogInformation(
                "Dojah detail lookup skipped because KYC {KycId} has no Dojah reference.",
                kyc.Id);
            return response;
        }

        try
        {
            response.DojahDetails = await dojahApiClient.GetVerificationAsync(
                kyc.DojahReference,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is ServiceUnavailableException or NotFoundException)
        {
            logger.LogWarning(
                exception,
                "Dojah verification details are unavailable for KYC {KycId}. " +
                "Returning local KYC information.",
                kyc.Id);
            response.DojahDetails = null;
        }

        return response;
    }

    public async Task<AdminVehicleResponseDto> GetVehicleAsync(
        Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await unitOfWork.AdminListings.GetVehicleDetailsAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);
        return MapVehicle(vehicle);
    }

    public async Task<PagedResponseDto<AdminWalletTransactionResponseDto>> GetWalletTransactionsAsync(
        AdminWalletTransactionsRequestDto request, CancellationToken cancellationToken = default)
    {
        request ??= new AdminWalletTransactionsRequestDto();
        ValidateDateRange(request.DateFrom, request.DateTo);
        var result = await unitOfWork.AdminListings.GetWalletTransactionsAsync(
            request.UserId, request.TransactionType, request.Status, request.DateFrom, request.DateTo,
            request.Reference, request.Search, request.PageNumber, request.PageSize, cancellationToken);
        return new PagedResponseDto<AdminWalletTransactionResponseDto>
        {
            Items = result.Items.Select(MapWalletTransaction).ToList(),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = (int)Math.Ceiling(result.TotalCount / (double)request.PageSize)
        };
    }

    public async Task<AdminDashboardResponseDto> GetDashboardAsync(int days = 7,
        CancellationToken cancellationToken = default)
    {
        var counts = await unitOfWork.AdminListings.GetDashboardCountsAsync(cancellationToken);
        var staff = await unitOfWork.AdminListings.GetStaffSummaryAsync(cancellationToken);
        var drivers = await unitOfWork.AdminListings.GetRecentDriverRequestsAsync(5, cancellationToken);
        var transactions = await unitOfWork.AdminListings.GetRecentWalletTransactionsAsync(5, cancellationToken);
        var finance = await financialService.GetSummaryAsync(cancellationToken);
        return new AdminDashboardResponseDto
        {
            TotalUsers = counts.TotalUsers,
            TotalDrivers = counts.TotalDrivers,
            ActiveDrivers = counts.ActiveDrivers,
            PendingDriverRequests = counts.PendingDriverRequests,
            PendingKycRequests = counts.PendingKycRequests,
            PendingVehicleDocumentRequests = counts.PendingVehicleDocumentRequests,
            TotalStaff = staff.TotalStaff,
            ActiveStaff = staff.ActiveStaff,
            PendingInvites = staff.PendingInvites,
            MonthlyPlatformEarnings = finance.MonthlyPlatformEarnings,
            TotalPlatformEarnings = finance.TotalPlatformEarnings,
            TotalTransactions = finance.TotalTransactions,
            RecentDriverRequests = drivers.Select(MapRecentDriver).ToList(),
            RecentTransactions = transactions.Select(MapWalletTransaction).ToList(),
            RevenueSummary = await financialService.GetRevenueSummaryAsync(days, cancellationToken)
        };
    }

    private async Task<StaffResponseDto> SetStaffStatusAsync(
        Guid staffId, UserStatus status, Guid? currentUserId, CancellationToken cancellationToken)
    {
        if (status == UserStatus.Deactivated && currentUserId == staffId)
            throw new ConflictException("A SuperAdmin cannot deactivate their own account.");
        var user = await GetStaffEntityAsync(staffId, cancellationToken);
        var roles = user.UserRoles.Select(userRole => userRole.Role.Name).ToList();
        if (status == UserStatus.Deactivated && roles.Contains(RoleNames.SuperAdmin))
        {
            var summary = await unitOfWork.AdminListings.GetStaffAsync(
                null, RoleNames.SuperAdmin, UserStatus.Active, 1, 2, cancellationToken);
            if (summary.TotalCount <= 1)
                throw new ConflictException("The final active SuperAdmin cannot be deactivated.");
        }
        var target = await unitOfWork.Users.GetByIdAsync(staffId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), staffId);
        target.Status = status;
        unitOfWork.Users.Update(target);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        user.Status = status;
        return MapStaff(user);
    }

    private async Task<AdminUserDetailResponseDto> SetCustomerStatusAsync(
        Guid userId, UserStatus status, bool requireDriver, CancellationToken cancellationToken)
    {
        var user = await GetCustomerAsync(userId, requireDriver, cancellationToken);
        var target = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);
        target.Status = status;
        unitOfWork.Users.Update(target);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        user.Status = status;
        return MapUserDetail(user);
    }

    private async Task<(
        AdminDriverDetailResponseDto Response,
        bool StatusChanged)> SetDriverStatusAsync(
        Guid driverId, UserStatus status, CancellationToken cancellationToken)
    {
        var user = await GetCustomerAsync(driverId, true, cancellationToken);
        var target = await unitOfWork.Users.GetByIdAsync(driverId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), driverId);
        var statusChanged = target.Status != status;
        target.Status = status;
        unitOfWork.Users.Update(target);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        user.Status = status;
        return (
            await GetDriverAsync(driverId, cancellationToken),
            statusChanged);
    }

    private async Task<User> GetStaffEntityAsync(Guid staffId, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.AdminListings.GetUserDetailsAsync(staffId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), staffId);
        if (!user.UserRoles.Any(userRole => IsStaffRole(userRole.Role.Name)))
            throw new NotFoundException("Staff", staffId);
        return user;
    }

    private async Task<User> GetCustomerAsync(
        Guid userId, bool requireDriver, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.AdminListings.GetUserDetailsAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);
        var roles = user.UserRoles.Select(userRole => userRole.Role.Name).ToList();
        if (roles.Any(IsStaffRole))
            throw new ForbiddenException("Staff accounts cannot be managed through customer endpoints.");
        if (requireDriver && !roles.Contains(RoleNames.Driver))
            throw new NotFoundException("Driver", userId);
        return user;
    }

    private static StaffResponseDto MapStaff(User user)
    {
        var firstName = user.Profile?.FirstName ?? string.Empty;
        var lastName = user.Profile?.LastName ?? string.Empty;
        return new StaffResponseDto
        {
            Id = user.Id,
            FirstName = firstName,
            LastName = lastName,
            FullName = $"{firstName} {lastName}".Trim(),
            Email = user.Email,
            Role = user.UserRoles.Select(userRole => userRole.Role.Name).FirstOrDefault(IsStaffRole) ?? string.Empty,
            Status = user.Status.ToString(),
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        };
    }

    private static AdminUserSummaryResponseDto MapUserSummary(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber,
        FirstName = user.Profile?.FirstName ?? string.Empty,
        LastName = user.Profile?.LastName ?? string.Empty,
        ProfilePhotoUrl = user.Profile?.ProfilePhotoUrl,
        Status = user.Status.ToString(),
        IsEmailVerified = user.IsEmailVerified,
        IsPhoneNumberVerified = user.IsPhoneNumberVerified,
        KycStatus = user.KycVerification?.Status.ToString() ?? "NotStarted",
        Roles = user.UserRoles.Select(userRole => userRole.Role.Name).Distinct().ToList(),
        CreatedAt = user.CreatedAt
    };

    private static AdminUserDetailResponseDto MapUserDetail(User user)
    {
        var summary = MapUserSummary(user);
        return new AdminUserDetailResponseDto
        {
            Id = summary.Id,
            Email = summary.Email,
            PhoneNumber = summary.PhoneNumber,
            FirstName = summary.FirstName,
            LastName = summary.LastName,
            ProfilePhotoUrl = summary.ProfilePhotoUrl,
            FullName = $"{summary.FirstName} {summary.LastName}".Trim(),
            Status = summary.Status,
            IsEmailVerified = summary.IsEmailVerified,
            IsPhoneNumberVerified = summary.IsPhoneNumberVerified,
            KycStatus = summary.KycStatus,
            Roles = summary.Roles,
            CreatedAt = summary.CreatedAt,
            Kyc = user.KycVerification is null ? null : MapKycBase(user.KycVerification)
        };
    }

    private static AdminDriverDetailResponseDto MapDriverDetail(
        User user,
        AdminDriverTripSummary tripSummary,
        RatingSummaryData ratingSummary)
    {
        var detail = MapUserDetail(user);
        var documentStatuses = user.Vehicles.SelectMany(vehicle => vehicle.Documents).Select(document => document.ReviewStatus).ToList();
        var documentStatus = documentStatuses.Count == 0 ? "NotSubmitted"
            : documentStatuses.Contains(VehicleDocumentReviewStatus.Rejected) ? "Rejected"
            : documentStatuses.Contains(VehicleDocumentReviewStatus.Pending) ? "Pending" : "Approved";
        return new AdminDriverDetailResponseDto
        {
            Id = detail.Id,
            Email = detail.Email,
            PhoneNumber = detail.PhoneNumber,
            FirstName = detail.FirstName,
            LastName = detail.LastName,
            ProfilePhotoUrl = detail.ProfilePhotoUrl,
            FullName = detail.FullName,
            Status = detail.Status,
            IsEmailVerified = detail.IsEmailVerified,
            IsPhoneNumberVerified = detail.IsPhoneNumberVerified,
            KycStatus = detail.KycStatus,
            Kyc = detail.Kyc,
            Roles = detail.Roles,
            CreatedAt = detail.CreatedAt,
            AverageRating = ratingSummary.AverageRating,
            RatingCount = ratingSummary.RatingCount,
            Vehicles = user.Vehicles.Select(MapVehicle).ToList(),
            VehicleDocumentStatus = documentStatus,
            TripSummary = new DriverTripSummaryResponseDto
            {
                TotalTrips = tripSummary.TotalTrips,
                ScheduledTrips = tripSummary.ScheduledTrips,
                CompletedTrips = tripSummary.CompletedTrips
            }
        };
    }

    private static AdminKycResponseDto MapKyc(KycVerification kyc)
    {
        var response = new AdminKycResponseDto
        {
            Email = kyc.User.Email,
            FirstName = kyc.User.Profile?.FirstName ?? string.Empty,
            LastName = kyc.User.Profile?.LastName ?? string.Empty,
            Roles = kyc.User.UserRoles.Select(userRole => userRole.Role.Name).Distinct().ToList()
        };
        CopyKyc(kyc, response);
        return response;
    }

    private static KycVerificationResponseDto MapKycBase(KycVerification kyc)
    {
        var response = new KycVerificationResponseDto();
        CopyKyc(kyc, response);
        return response;
    }

    private static void CopyKyc(KycVerification source, KycVerificationResponseDto target)
    {
        target.Id = source.Id;
        target.UserId = source.UserId;
        target.BiometricVerificationUrl = source.BiometricVerificationUrl;
        target.DriverLicenseUrl = source.DriverLicenseUrl;
        target.SecondaryIdentificationUrl = source.SecondaryIdentificationUrl;
        target.Status = source.Status;
        target.VerifiedAt = source.VerifiedAt;
        target.ProviderName = source.ProviderName;
        target.ProviderReference = source.ProviderReference;
        target.DojahReference = source.DojahReference;
        target.ProviderStatus = source.ProviderStatus;
        target.RejectionReason = source.RejectionReason;
        target.LastProviderUpdatedAt = source.LastProviderUpdatedAt;
    }

    private static AdminVehicleResponseDto MapVehicle(Vehicle vehicle) => new()
    {
        Id = vehicle.Id,
        UserId = vehicle.UserId,
        OwnerEmail = vehicle.User?.Email ?? string.Empty,
        OwnerName = $"{vehicle.User?.Profile?.FirstName} {vehicle.User?.Profile?.LastName}".Trim(),
        LicensePlateNumber = vehicle.LicensePlateNumber,
        VehicleOwnerName = vehicle.VehicleOwnerName,
        RegistrationType = vehicle.RegistrationType,
        VehicleType = vehicle.VehicleType,
        Make = vehicle.Make,
        Model = vehicle.Model,
        ManufacturingYear = vehicle.ManufacturingYear,
        Colour = vehicle.Colour,
        WalkAroundVideoUrl = vehicle.WalkAroundVideoUrl,
        PassengerSeatCount = vehicle.PassengerSeatCount,
        LuggageCapacity = vehicle.LuggageCapacity,
        Amenities = vehicle.Amenities.Select(x => x.AmenityType).Order().ToList(),
        AdditionalDetails = vehicle.AdditionalDetails,
        OnboardingStatus = vehicle.OnboardingStatus,
        RejectionReason = vehicle.RejectionReason,
        Capacity = vehicle.Capacity,
        IsActive = vehicle.IsActive,
        ImageUrls = vehicle.Images.Select(image => image.ImageUrl).ToList(),
        Images = vehicle.Images.Select(image => new VehicleImageResponseDto
        {
            Id = image.Id,
            ImageUrl = image.ImageUrl,
            ImageType = image.ImageType,
            IsPrimary = image.IsPrimary
        }).ToList(),
        Documents = vehicle.Documents.Select(document => new VehicleDocumentResponseDto
        {
            Id = document.Id,
            VehicleId = document.VehicleId,
            DocumentType = document.DocumentType,
            DocumentUrl = document.DocumentUrl,
            ExpiryDate = document.ExpiryDate,
            ReviewStatus = document.ReviewStatus,
            ReviewedBy = document.ReviewedBy,
            ReviewedAt = document.ReviewedAt,
            RejectionReason = document.RejectionReason
        }).ToList()
    };

    private static AdminWalletTransactionResponseDto MapWalletTransaction(WalletTransaction transaction) => new()
    {
        Id = transaction.Id,
        UserId = transaction.Wallet.UserId,
        UserName = $"{transaction.Wallet.User.Profile?.FirstName} {transaction.Wallet.User.Profile?.LastName}".Trim(),
        Email = transaction.Wallet.User.Email,
        Amount = transaction.Amount,
        TransactionType = transaction.Type,
        Reference = transaction.Reference,
        CreatedAt = transaction.CreatedAt
    };

    private static RecentDriverRequestResponseDto MapRecentDriver(User user) => new()
    {
        DriverId = user.Id,
        FullName = $"{user.Profile?.FirstName} {user.Profile?.LastName}".Trim(),
        Email = user.Email,
        Status = user.Status.ToString(),
        KycStatus = user.KycVerification?.Status.ToString() ?? "NotStarted",
        CreatedAt = user.CreatedAt
    };

    private static void ValidateInvitation(InviteStaffRequestDto request)
    {
        if (request is null) throw new ValidationException("Request is required.");
        if (string.IsNullOrWhiteSpace(request.FirstName)) throw new ValidationException("First name is required.");
        if (string.IsNullOrWhiteSpace(request.LastName)) throw new ValidationException("Last name is required.");
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@')) throw new ValidationException("A valid email is required.");
        if (string.IsNullOrWhiteSpace(request.Role)) throw new ValidationException("Role is required.");
    }

    private static string NormalizeStaffRole(string role) =>
        role.Trim().Equals(RoleNames.Admin, StringComparison.OrdinalIgnoreCase) ? RoleNames.Admin
        : role.Trim().Equals(RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase) ? RoleNames.SuperAdmin
        : throw new ValidationException("Role must be Admin or SuperAdmin.");

    private static bool IsStaffRole(string role) =>
        role is RoleNames.Admin or RoleNames.SuperAdmin;

    private static string HashCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private static void ValidateDateRange(DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new ValidationException("DateFrom cannot be later than DateTo.");
    }
}
