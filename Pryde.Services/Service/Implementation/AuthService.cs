using Mapster;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Notifications.Interface;
using Pryde.Services.Security.Implementation;
using Pryde.Services.Security.Interface;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;

namespace Pryde.Services.Service.Implementation;


public class AuthService(
    IUnitOfWork unitOfWork,IPasswordHasher passwordHasher,IJwtService jwtService,
    IEmailService emailService, IWalletService walletService, ILogger<AuthService> logger,
    IOptions<EmailSettings> emailOptions,
    IOnboardingStatusService onboardingStatusService) : IAuthService
{
    private const int ResendCooldownSeconds = 60;
    private const int MaximumVerificationAttempts = 5;
    private const int MaximumResendsPerHour = 5;
    private const string GenericResendMessage =
        "If the account requires email verification, a verification code has been sent.";
    private readonly EmailSettings _emailSettings = emailOptions.Value;

    public async Task<RegisterResponseDto> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateRegistrationRequest(request);

        var email = request.Email.Trim().ToLowerInvariant();
        var phoneNumber = request.PhoneNumber.Trim();

        var exists = await unitOfWork.Users.ExistsAsync(
            email,
            phoneNumber,
            cancellationToken);

        if (exists)
        {
            throw new ConflictException(
                "A user with this email or phone number already exists.");
        }

        var user = new User
        {
            Email = email,
            PhoneNumber = phoneNumber,
            PasswordHash = passwordHasher.Hash(request.Password),
            IsEmailVerified = false,
            IsPhoneNumberVerified = false,
            IsTwoFactorEnabled = false,
            Status = UserStatus.Pending
        };

        await unitOfWork.Users.CreateAsync(user, cancellationToken);

        var profile = new Profile
        {
            UserId = user.Id,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim()
        };

        await unitOfWork.Profiles.CreateAsync(profile, cancellationToken);

        var (verificationCode, rawVerificationCode) = await CreateEmailVerificationCodeAsync(
            user.Id,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        try
        {
            await SendEmailVerificationCodeAsync(
                user.Email,
                profile.FirstName,
                rawVerificationCode,
                cancellationToken);
        }
        catch (Exception ex)
        {
            verificationCode.ConsumedAt = DateTime.UtcNow;
            unitOfWork.VerificationCodes.Update(verificationCode);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogError(
                ex,
                "Failed to send an email verification code for user {UserId}.",
                user.Id);
            throw new ServiceUnavailableException(
                "Email verification is temporarily unavailable. Please try again later.");
        }

        var response = user.Adapt<RegisterResponseDto>();
        response.EmailVerificationRequired = !user.IsEmailVerified;
        response.NextAction = WorkflowNextAction.VerifyEmail;
        response.RequiredActor = WorkflowActor.User;
        return response;
    }

    public async Task<LoginResponseDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateLoginRequest(request);

        var identifier = request.EmailOrPhone.Trim();

        if (identifier.Contains('@'))
        {
            identifier = identifier.ToLowerInvariant();
        }

        var user = identifier.Contains('@')
            ? await unitOfWork.Users.GetByEmailAsync(identifier, cancellationToken)
            : await unitOfWork.Users.GetByPhoneNumberAsync(identifier, cancellationToken);

        if (user is null ||
            !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException(
                "Invalid email/phone number or password.");
        }

        EnsureUserCanAuthenticate(user);

        var userRoles = await unitOfWork.UserRoles.GetByUserIdAsync(
            user.Id,
            cancellationToken);

        var roleNames = userRoles
            .Select(ur => ur.Role.Name)
            .Distinct()
            .ToList();

        var response = await IssueTokensAsync(user, roleNames, cancellationToken);

        user.LastLoginAt = DateTime.UtcNow;
        unitOfWork.Users.Update(user);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }

    public async Task<LoginResponseDto> SelectRolesAsync(
        Guid userId,
        SelectRolesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateRoleSelectionRequest(request);
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);
        EnsureUserCanAuthenticate(user);

        var currentRoles = await unitOfWork.UserRoles.GetByUserIdAsync(
            user.Id, cancellationToken);
        var roleNames = currentRoles
            .Select(userRole => userRole.Role.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var roleType in request.Roles)
        {
            var roleName = roleType.ToString();
            if (roleNames.Contains(roleName))
                continue;

            var role = await unitOfWork.Roles.GetByNameAsync(
                roleName, cancellationToken)
                ?? throw new NotFoundException(nameof(Role), roleName);
            await unitOfWork.UserRoles.CreateAsync(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            }, cancellationToken);
            roleNames.Add(role.Name);
        }

        if (!await unitOfWork.KycVerifications.ExistsForUserAsync(
                user.Id, cancellationToken))
        {
            await unitOfWork.KycVerifications.CreateAsync(new KycVerification
            {
                UserId = user.Id,
                Status = KycStatus.Pending
            }, cancellationToken);
        }

        var profile = await unitOfWork.Profiles.GetByUserIdAsync(
            user.Id, cancellationToken);
        var accountName = profile is null
            ? user.Email
            : $"{profile.FirstName} {profile.LastName}".Trim();
        await walletService.CreateWalletForUserAsync(
            user, accountName, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var response = await IssueTokensAsync(user, roleNames, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        ApplyOnboardingWorkflow(response);
        return response;
    }

    public async Task<LoginResponseDto> RefreshTokenAsync(
        RefreshTokenRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            throw new ValidationException("Refresh token is required.");
        }

        var tokenHash = jwtService.HashToken(request.RefreshToken);

        var storedToken = await unitOfWork.RefreshTokens.GetByTokenHashAsync(
            tokenHash,
            cancellationToken);

        if (storedToken is null || !storedToken.IsActive)
        {
            throw new UnauthorizedException("Refresh token is invalid or has expired.");
        }

        var user = await unitOfWork.Users.GetByIdAsync(
            storedToken.UserId,
            cancellationToken)
            ?? throw new UnauthorizedException("Refresh token is invalid or has expired.");

        EnsureUserCanAuthenticate(user);

        unitOfWork.RefreshTokens.Revoke(storedToken);

        var userRoles = await unitOfWork.UserRoles.GetByUserIdAsync(
            user.Id,
            cancellationToken);

        var roleNames = userRoles
            .Select(ur => ur.Role.Name)
            .Distinct()
            .ToList();

        var response = await IssueTokensAsync(user, roleNames, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }

    public async Task LogoutAsync(
        RefreshTokenRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            throw new BadRequestException("Refresh token is required.");
        }

        var tokenHash = jwtService.HashToken(request.RefreshToken);

        var storedToken = await unitOfWork.RefreshTokens.GetByTokenHashAsync(
            tokenHash,
            cancellationToken);

        if (storedToken is null || storedToken.IsRevoked)
        {
            throw new UnauthorizedException("Invalid or revoked refresh token.");

        }

        unitOfWork.RefreshTokens.Revoke(storedToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<LoginResponseDto> IssueTokensAsync(
        User user,
        IEnumerable<string> roleNames,
        CancellationToken cancellationToken)
    {
        var accessToken = jwtService.GenerateAccessToken(
            user.Id,
            user.Email,
            roleNames);

        var refreshToken = jwtService.GenerateRefreshToken();

        await unitOfWork.RefreshTokens.CreateAsync(
            new RefreshToken
            {
                UserId = user.Id,
                TokenHash = jwtService.HashToken(refreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(jwtService.RefreshTokenExpiryDays)
            },
            cancellationToken);

        var response = user.Adapt<LoginResponseDto>();
        response.AccessToken = accessToken;
        response.RefreshToken = refreshToken;
        response.Onboarding = await onboardingStatusService.GetAsync(
            user.Id,
            cancellationToken);
        return response;
    }
    public async Task ForgotPasswordAsync(
        ForgotPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Email))
            throw new ValidationException("Email is required.");

        var user = await unitOfWork.Users.GetByEmailAsync(
            request.Email.Trim().ToLowerInvariant(), cancellationToken);

        if (user is null) return;

        await unitOfWork.PasswordResetCodes.InvalidateAllForUserAsync(user.Id, cancellationToken);

        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        await unitOfWork.PasswordResetCodes.CreateAsync(
            new PasswordResetCode
            {
                UserId = user.Id,
                CodeHash = HashCode(code),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            },
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailService.SendAsync(
            user.Email,
            "Reset your Pryde password",
            $"<p>Your password reset code is <strong>{code}</strong>. It expires in 10 minutes.</p>",
            cancellationToken);
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Email) ||
            string.IsNullOrWhiteSpace(request.Code) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new ValidationException("Email, code, and new password are all required.");
        }

        var user = await unitOfWork.Users.GetByEmailAsync(
            request.Email.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new ValidationException("Invalid or expired reset code.");

        var code = await unitOfWork.PasswordResetCodes.GetLatestActiveByUserIdAsync(
            user.Id, cancellationToken);

        if (code is null || !code.IsValid || code.CodeHash != HashCode(request.Code))
            throw new ValidationException("Invalid or expired reset code.");

        unitOfWork.PasswordResetCodes.MarkUsed(code);
        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        var roles = await unitOfWork.UserRoles.GetByUserIdAsync(user.Id, cancellationToken);
        if (user.Status == UserStatus.Pending && roles.Any(userRole =>
                userRole.Role.Name is "Admin" or "SuperAdmin"))
        {
            user.Status = UserStatus.Active;
            user.IsEmailVerified = true;
        }
        unitOfWork.Users.Update(user);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<EmailVerificationResendResponseDto> ResendEmailVerificationAsync(
        EmailVerificationResendRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Email) || !request.Email.Contains('@'))
            throw new ValidationException("A valid email is required.");

        var now = DateTime.UtcNow;
        var user = await unitOfWork.Users.GetByEmailAsync(
            request.Email.Trim().ToLowerInvariant(), cancellationToken);

        if (user is null || user.IsEmailVerified)
            return ResendResponse(now.AddSeconds(ResendCooldownSeconds), now);

        var latest = await unitOfWork.VerificationCodes.GetLatestActiveAsync(
            user.Id,
            VerificationCodePurpose.EmailAccountVerification,
            VerificationChannel.Email,
            cancellationToken);

        if (latest is not null)
        {
            var cooldownEndsAt = latest.LastSentAt.AddSeconds(ResendCooldownSeconds);
            if (cooldownEndsAt > now)
                return ResendResponse(cooldownEndsAt, now);
        }

        var recentCount = await unitOfWork.VerificationCodes.CountCreatedSinceAsync(
            user.Id,
            VerificationCodePurpose.EmailAccountVerification,
            VerificationChannel.Email,
            now.AddHours(-1),
            cancellationToken);

        if (recentCount >= MaximumResendsPerHour)
        {
            var resendAvailableAt = (latest?.LastSentAt ?? now).AddHours(1);
            return ResendResponse(resendAvailableAt, now);
        }

        var (verificationCode, rawCode) = await CreateEmailVerificationCodeAsync(
            user.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await SendEmailVerificationCodeAsync(
                user.Email, null, rawCode, cancellationToken);
        }
        catch (Exception exception)
        {
            verificationCode.ConsumedAt = DateTime.UtcNow;
            unitOfWork.VerificationCodes.Update(verificationCode);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogError(
                exception,
                "Failed to resend an email verification code for user {UserId}.",
                user.Id);
            throw new ServiceUnavailableException(
                "Email verification is temporarily unavailable. Please try again later.");
        }

        return ResendResponse(
            verificationCode.LastSentAt.AddSeconds(ResendCooldownSeconds), now);
    }

    public async Task<VerificationStatusResponseDto> VerifyEmailAsync(
        EmailVerificationVerifyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateEmailVerificationRequest(request);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var result = await unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
        {
            var user = await unitOfWork.Users.GetByEmailAsync(
                normalizedEmail, transactionToken);
            if (user is null)
            {
                return (
                    Succeeded: false,
                    UserId: Guid.Empty,
                    Status: UserStatus.Pending);
            }

            if (user.IsEmailVerified)
            {
                return (
                    Succeeded: true,
                    UserId: user.Id,
                    Status: user.Status);
            }

            var verificationCode = await unitOfWork.VerificationCodes.GetLatestActiveAsync(
                user.Id,
                VerificationCodePurpose.EmailAccountVerification,
                VerificationChannel.Email,
                transactionToken);

            var now = DateTime.UtcNow;
            if (verificationCode is null ||
                verificationCode.ConsumedAt.HasValue ||
                verificationCode.ExpiresAt <= now ||
                verificationCode.AttemptCount >= MaximumVerificationAttempts)
            {
                return (
                    Succeeded: false,
                    UserId: user.Id,
                    Status: user.Status);
            }

            if (!VerificationCodeSecurity.Matches(
                    user.Id,
                    VerificationCodePurpose.EmailAccountVerification,
                    request.Code,
                    verificationCode.CodeHash))
            {
                verificationCode.AttemptCount++;
                if (verificationCode.AttemptCount >= MaximumVerificationAttempts)
                    verificationCode.ConsumedAt = now;

                unitOfWork.VerificationCodes.Update(verificationCode);
                await unitOfWork.SaveChangesAsync(transactionToken);
                return (
                    Succeeded: false,
                    UserId: user.Id,
                    Status: user.Status);
            }

            verificationCode.ConsumedAt = now;
            user.IsEmailVerified = true;
            unitOfWork.VerificationCodes.Update(verificationCode);
            unitOfWork.Users.Update(user);
            await unitOfWork.SaveChangesAsync(transactionToken);
            return (
                Succeeded: true,
                UserId: user.Id,
                Status: user.Status);
        }, cancellationToken);

        if (!result.Succeeded)
            throw new ValidationException("Invalid or expired verification code.");

        var response = await GetVerificationStatusAsync(
            result.UserId,
            cancellationToken);
        response.WorkflowStatus = result.Status;
        response.NextAction = WorkflowNextAction.Login;
        response.RequiredActor = WorkflowActor.User;
        return response;
    }

    public async Task<VerificationStatusResponseDto> GetVerificationStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);
        var latest = await unitOfWork.VerificationCodes.GetLatestActiveAsync(
            user.Id,
            VerificationCodePurpose.EmailAccountVerification,
            VerificationChannel.Email,
            cancellationToken);

        var now = DateTime.UtcNow;
        DateTime? resendAvailableAt = user.IsEmailVerified || latest is null
            ? null
            : latest.LastSentAt.AddSeconds(ResendCooldownSeconds);
        var activeCode = latest is not null &&
                         latest.ConsumedAt is null &&
                         latest.ExpiresAt > now;

        return new VerificationStatusResponseDto
        {
            IsEmailVerified = user.IsEmailVerified,
            IsPhoneNumberVerified = user.IsPhoneNumberVerified,
            EmailVerificationRequired = !user.IsEmailVerified,
            ResendAvailableAt = resendAvailableAt,
            ResendCooldownSeconds = resendAvailableAt.HasValue
                ? RemainingSeconds(resendAvailableAt.Value, now)
                : 0,
            VerificationCodeExpiresAt = activeCode ? latest!.ExpiresAt : null
        };
    }

    private async Task<(VerificationCode VerificationCode, string RawCode)>
        CreateEmailVerificationCodeAsync(
            Guid userId,
            CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await unitOfWork.VerificationCodes.InvalidateUnusedAsync(
            userId,
            VerificationCodePurpose.EmailAccountVerification,
            VerificationChannel.Email,
            now,
            cancellationToken);

        var rawCode = VerificationCodeSecurity.GenerateSixDigitCode();
        var verificationCode = new VerificationCode
        {
            UserId = userId,
            Purpose = VerificationCodePurpose.EmailAccountVerification,
            Channel = VerificationChannel.Email,
            CodeHash = VerificationCodeSecurity.Hash(
                userId,
                VerificationCodePurpose.EmailAccountVerification,
                rawCode),
            ExpiresAt = now.AddMinutes(_emailSettings.OtpExpiryMinutes),
            LastSentAt = now,
            CreatedAt = now
        };

        await unitOfWork.VerificationCodes.CreateAsync(
            verificationCode, cancellationToken);
        return (verificationCode, rawCode);
    }

    private Task SendEmailVerificationCodeAsync(
        string email,
        string? firstName,
        string code,
        CancellationToken cancellationToken)
    {
        var greeting = string.IsNullOrWhiteSpace(firstName)
            ? string.Empty
            : $"<p>Hello {firstName},</p>";
        return emailService.SendAsync(
            email,
            "Verify your Pryde email",
            $"{greeting}<p>Welcome to Pryde. Your email verification code is <strong>{code}</strong>. " +
            $"It expires in {_emailSettings.OtpExpiryMinutes} minutes.</p>",
            cancellationToken);
    }

    private static EmailVerificationResendResponseDto ResendResponse(
        DateTime resendAvailableAt,
        DateTime now) => new()
    {
        Message = GenericResendMessage,
        ResendAvailableAt = resendAvailableAt,
        ResendCooldownSeconds = RemainingSeconds(resendAvailableAt, now),
        Status = WorkflowOperationStatus.Accepted,
        NextAction = WorkflowNextAction.VerifyEmail,
        RequiredActor = WorkflowActor.User
    };

    private static void ApplyOnboardingWorkflow(
        LoginResponseDto response)
    {
        response.WorkflowStatus = response.Onboarding.CurrentStage;

        switch (response.Onboarding.CurrentStage)
        {
            case OnboardingStage.RoleSelection:
            {
                response.NextAction = WorkflowNextAction.SelectRole;
                response.RequiredActor = WorkflowActor.User;
                break;
            }
            case OnboardingStage.IdentityVerification:
            {
                response.NextAction = WorkflowNextAction.CompleteKyc;
                response.RequiredActor = WorkflowActor.User;
                break;
            }
            case OnboardingStage.DriverDocuments:
            case OnboardingStage.VehicleInformation:
            {
                response.NextAction =
                    WorkflowNextAction.CompleteVehicleOnboarding;
                response.RequiredActor = WorkflowActor.Driver;
                break;
            }
            case OnboardingStage.SubmittedForReview:
            {
                response.NextAction =
                    WorkflowNextAction.AwaitAdminApproval;
                response.RequiredActor = WorkflowActor.Admin;
                break;
            }
            case OnboardingStage.Completed:
            {
                if (response.Onboarding.DriverAccessGranted)
                {
                    response.NextAction = WorkflowNextAction.CreateTrip;
                    response.RequiredActor = WorkflowActor.Driver;
                }
                else
                {
                    response.NextAction = WorkflowNextAction.None;
                    response.RequiredActor = WorkflowActor.None;
                }

                break;
            }
            default:
            {
                response.NextAction = WorkflowNextAction.None;
                response.RequiredActor = WorkflowActor.None;
                break;
            }
        }
    }

    private static int RemainingSeconds(DateTime availableAt, DateTime now) =>
        Math.Max(0, (int)Math.Ceiling((availableAt - now).TotalSeconds));

    private static void ValidateEmailVerificationRequest(
        EmailVerificationVerifyRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request?.Email) || !request.Email.Contains('@'))
            throw new ValidationException("A valid email is required.");

        if (request.Code is null ||
            request.Code.Length != 6 ||
            !request.Code.All(char.IsDigit))
        {
            throw new ValidationException("A six-digit verification code is required.");
        }
    }

    private static string HashCode(string code)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(code);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    private static void ValidateRegistrationRequest(RegisterRequestDto request)
    {
        if (request is null)
            throw new ValidationException("Request cannot be null.");

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ValidationException("Email is required.");

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            throw new ValidationException("Phone number is required.");

        if (string.IsNullOrWhiteSpace(request.FirstName))
            throw new ValidationException("First name is required.");

        if (string.IsNullOrWhiteSpace(request.LastName))
            throw new ValidationException("Last name is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("Password cannot be empty.");

    }

    private static void ValidateRoleSelectionRequest(SelectRolesRequestDto request)
    {
        if (request?.Roles is null || request.Roles.Count == 0)
            throw new ValidationException(
                "At least one role (Passenger or Driver) must be selected.");
        if (request.Roles.Distinct().Count() != request.Roles.Count)
            throw new ValidationException("Duplicate roles are not allowed.");
        if (request.Roles.Any(role =>
                role is not (RoleType.Passenger or RoleType.Driver)))
            throw new ValidationException(
                "Only Passenger or Driver roles may be self-assigned.");
    }

    private static void EnsureUserCanAuthenticate(User user)
    {
        if (user.Status == UserStatus.Suspended)
            throw new ForbiddenException("This account has been suspended.");
        if (user.Status == UserStatus.Deactivated)
            throw new ForbiddenException("This account has been deactivated.");
        if (!user.IsEmailVerified)
            throw new ForbiddenException(
                "Email verification is required before login.");
    }

    private static void ValidateLoginRequest(LoginRequestDto request)
    {
        if (request is null)
            throw new ValidationException("Request cannot be null.");

        if (string.IsNullOrWhiteSpace(request.EmailOrPhone))
            throw new ValidationException(
                "Email or phone number is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException(
                "Password is required.");
    }
}
