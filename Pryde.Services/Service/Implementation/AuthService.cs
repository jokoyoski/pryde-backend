using Mapster;
using Pryde.Domain.Common.Exceptions;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Security.Interface;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;


public class AuthService(
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IJwtService jwtService) : IAuthService
{
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

        var assignedRoles = request.Roles
            .Distinct()
            .ToList();

        foreach (var roleType in assignedRoles)
        {
            var role = await unitOfWork.Roles.GetByNameAsync(
                roleType.ToString(),
                cancellationToken)
                ?? throw new NotFoundException(nameof(Role), roleType);

            await unitOfWork.UserRoles.CreateAsync(
                new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id
                },
                cancellationToken);
        }

        await unitOfWork.KycVerifications.CreateAsync(
            new KycVerification
            {
                UserId = user.Id,
                Status = KycStatus.Pending
            },
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = user.Adapt<RegisterResponseDto>();
        response.Roles = assignedRoles;
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

        if (user.Status == UserStatus.Suspended)
        {
            throw new ForbiddenException(
                "This account has been suspended.");
        }

        if (user.Status == UserStatus.Deactivated)
        {
            throw new ForbiddenException(
                "This account has been deactivated.");
        }

        var userRoles = await unitOfWork.UserRoles.GetByUserIdAsync(
            user.Id,
            cancellationToken);

        var roleNames = userRoles
            .Select(ur => ur.Role.Name)
            .Distinct()
            .ToList();

        var accessToken = jwtService.GenerateToken(
            user.Id,
            user.Email,
            roleNames);

        var response = user.Adapt<LoginResponseDto>();
        response.AccessToken = accessToken;
        return response;
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

        if (request.Roles is null || request.Roles.Count == 0)
            throw new ValidationException(
                "At least one role (Passenger or Driver) must be selected.");

        if (request.Roles.Distinct().Count() != request.Roles.Count)
            throw new ValidationException(
                "Duplicate roles are not allowed.");

        if (request.Roles.Contains(RoleType.Admin))
            throw new ValidationException(
                "Admin role cannot be self-assigned during registration.");
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