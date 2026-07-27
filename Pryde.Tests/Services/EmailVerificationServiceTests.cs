using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Notifications.Interface;
using Pryde.Services.Security.Implementation;
using Pryde.Services.Security.Interface;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Settings;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class EmailVerificationServiceTests
{
    [Fact]
    public async Task RegistrationCreatesAndSendsEmailVerificationOtp()
    {
        var context = TestContext();

        var response = await context.Service.RegisterAsync(Registration());

        var code = Assert.Single(context.UnitOfWork.VerificationCodeRepository.Items);
        var sent = Assert.Single(context.Email.Messages);
        Assert.True(response.EmailVerificationRequired);
        Assert.Equal(VerificationCodePurpose.EmailAccountVerification, code.Purpose);
        Assert.Equal(VerificationChannel.Email, code.Channel);
        Assert.DoesNotContain(sent.Code, code.CodeHash);
        Assert.Equal(64, code.CodeHash.Length);
        Assert.Empty(context.UnitOfWork.UserRoleRepository.Items);
        Assert.Empty(((TestRefreshTokenRepository)context.UnitOfWork.RefreshTokens).Items);
        Assert.Empty(((TestKycVerificationRepository)
            context.UnitOfWork.KycVerifications).Items);
        Assert.Empty(context.UnitOfWork.WalletRepository.Items);
        Assert.Empty(context.UnitOfWork.VirtualAccountRepository.Items);
        Assert.Null(typeof(RegisterResponseDto).GetProperty("AccessToken"));
        Assert.Null(typeof(RegisterResponseDto).GetProperty("RefreshToken"));
        Assert.Null(typeof(RegisterResponseDto).GetProperty("Roles"));
        Assert.False(((TestUserRepository)context.UnitOfWork.Users)
            .Items.Single().IsPhoneNumberVerified);
    }

    [Fact]
    public async Task CorrectOtpVerifiesEmailAndLeavesPhoneUnverified()
    {
        var context = TestContextWithUser();
        await context.Service.ResendEmailVerificationAsync(Resend(context.User.Email));
        var code = context.Email.Messages.Single().Code;

        var result = await context.Service.VerifyEmailAsync(Verify(context.User.Email, code));

        Assert.True(result.IsEmailVerified);
        Assert.False(result.IsPhoneNumberVerified);
        Assert.False(result.EmailVerificationRequired);
        Assert.NotNull(context.UnitOfWork.VerificationCodeRepository.Items.Single().ConsumedAt);
    }

    [Fact]
    public async Task UnverifiedLoginIsForbiddenAndDoesNotIssueTokens()
    {
        var context = TestContextWithUser();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            context.Service.LoginAsync(Login(context.User.Email)));

        Assert.Equal(0, context.Jwt.AccessTokensGenerated);
        Assert.Empty(((TestRefreshTokenRepository)context.UnitOfWork.RefreshTokens).Items);
    }

    [Fact]
    public async Task VerifiedLoginSucceedsAsBefore()
    {
        var context = TestContextWithUser();
        context.User.IsEmailVerified = true;

        var response = await context.Service.LoginAsync(Login(context.User.Email));

        Assert.Equal("access-token", response.AccessToken);
        Assert.NotEmpty(response.RefreshToken);
        Assert.Equal(1, context.Jwt.AccessTokensGenerated);
        Assert.Single(((TestRefreshTokenRepository)context.UnitOfWork.RefreshTokens).Items);
    }

    [Fact]
    public async Task VerifiedUserSelectsRoleAfterLoginAndReceivesUpdatedTokens()
    {
        var context = TestContextWithUser();
        context.User.IsEmailVerified = true;
        await context.Service.LoginAsync(Login(context.User.Email));

        var response = await context.Service.SelectRolesAsync(
            context.User.Id,
            new SelectRolesRequestDto { Roles = [RoleType.Driver] });

        var assignment = Assert.Single(context.UnitOfWork.UserRoleRepository.Items);
        Assert.Equal(RoleType.Driver.ToString(), assignment.Role.Name);
        Assert.Contains(RoleType.Driver.ToString(), context.Jwt.LastRoles);
        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal(2, context.Jwt.AccessTokensGenerated);
        Assert.Single(((TestKycVerificationRepository)
            context.UnitOfWork.KycVerifications).Items);
        Assert.Single(context.UnitOfWork.WalletRepository.Items);
        Assert.Single(context.UnitOfWork.VirtualAccountRepository.Items);
    }

    [Theory]
    [InlineData(RoleType.Admin)]
    [InlineData((RoleType)4)]
    public async Task RoleSelectionRejectsAdministrativeRoles(RoleType role)
    {
        var context = TestContextWithUser();
        context.User.IsEmailVerified = true;

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.SelectRolesAsync(
                context.User.Id,
                new SelectRolesRequestDto { Roles = [role] }));

        Assert.Empty(context.UnitOfWork.UserRoleRepository.Items);
        Assert.Equal(0, context.Jwt.AccessTokensGenerated);
    }

    [Fact]
    public async Task IncorrectOtpIncrementsAttemptCount()
    {
        var context = TestContextWithUser();
        await context.Service.ResendEmailVerificationAsync(Resend(context.User.Email));

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.VerifyEmailAsync(Verify(context.User.Email, "000000")));

        Assert.Equal(1, context.UnitOfWork.VerificationCodeRepository.Items.Single().AttemptCount);
        Assert.False(context.User.IsEmailVerified);
    }

    [Fact]
    public async Task ExpiredOtpIsRejected()
    {
        var context = TestContextWithUser();
        await context.Service.ResendEmailVerificationAsync(Resend(context.User.Email));
        var code = context.Email.Messages.Single().Code;
        context.UnitOfWork.VerificationCodeRepository.Items.Single().ExpiresAt = DateTime.UtcNow.AddSeconds(-1);

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.VerifyEmailAsync(Verify(context.User.Email, code)));

        Assert.False(context.User.IsEmailVerified);
    }

    [Fact]
    public async Task ConsumedOtpIsRejected()
    {
        var context = TestContextWithUser();
        await context.Service.ResendEmailVerificationAsync(Resend(context.User.Email));
        var code = context.Email.Messages.Single().Code;
        context.UnitOfWork.VerificationCodeRepository.Items.Single().ConsumedAt = DateTime.UtcNow;

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.VerifyEmailAsync(Verify(context.User.Email, code)));
    }

    [Fact]
    public async Task VerificationUsesLatestActiveOtpInsteadOfNewerConsumedCode()
    {
        var context = TestContextWithUser();
        await context.Service.ResendEmailVerificationAsync(Resend(context.User.Email));
        var activeCode = context.Email.Messages.Single().Code;
        context.UnitOfWork.VerificationCodeRepository.Items.Add(new VerificationCode
        {
            UserId = context.User.Id,
            Purpose = VerificationCodePurpose.EmailAccountVerification,
            Channel = VerificationChannel.Email,
            CodeHash = new string('0', 64),
            CreatedAt = DateTime.UtcNow.AddSeconds(1),
            LastSentAt = DateTime.UtcNow.AddSeconds(1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            ConsumedAt = DateTime.UtcNow
        });

        var result = await context.Service.VerifyEmailAsync(
            Verify(context.User.Email, activeCode));

        Assert.True(result.IsEmailVerified);
    }

    [Fact]
    public async Task ResendSupersedesPreviousOtp()
    {
        var context = TestContextWithUser();
        await context.Service.ResendEmailVerificationAsync(Resend(context.User.Email));
        var firstCode = context.Email.Messages.Single().Code;
        context.UnitOfWork.VerificationCodeRepository.Items.Single().LastSentAt =
            DateTime.UtcNow.AddSeconds(-61);

        await context.Service.ResendEmailVerificationAsync(Resend(context.User.Email));

        Assert.Equal(2, context.UnitOfWork.VerificationCodeRepository.Items.Count);
        Assert.NotNull(context.UnitOfWork.VerificationCodeRepository.Items[0].ConsumedAt);
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.VerifyEmailAsync(Verify(context.User.Email, firstCode)));
    }

    [Fact]
    public async Task ResendCooldownReturnsRemainingSecondsWithoutSendingAgain()
    {
        var context = TestContextWithUser();
        await context.Service.ResendEmailVerificationAsync(Resend(context.User.Email));

        var result = await context.Service.ResendEmailVerificationAsync(Resend(context.User.Email));

        Assert.InRange(result.ResendCooldownSeconds, 1, 60);
        Assert.Single(context.Email.Messages);
        Assert.Single(context.UnitOfWork.VerificationCodeRepository.Items);
    }

    [Fact]
    public async Task ResendAfterCooldownCreatesNewOtp()
    {
        var context = TestContextWithUser();
        await context.Service.ResendEmailVerificationAsync(Resend(context.User.Email));
        context.UnitOfWork.VerificationCodeRepository.Items.Single().LastSentAt =
            DateTime.UtcNow.AddSeconds(-61);

        await context.Service.ResendEmailVerificationAsync(Resend(context.User.Email));

        Assert.Equal(2, context.Email.Messages.Count);
        Assert.Equal(2, context.UnitOfWork.VerificationCodeRepository.Items.Count);
    }

    [Fact]
    public async Task MaximumVerificationAttemptsConsumesOtp()
    {
        var context = TestContextWithUser();
        await context.Service.ResendEmailVerificationAsync(Resend(context.User.Email));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                context.Service.VerifyEmailAsync(Verify(context.User.Email, "000000")));
        }

        var code = context.UnitOfWork.VerificationCodeRepository.Items.Single();
        Assert.Equal(5, code.AttemptCount);
        Assert.NotNull(code.ConsumedAt);
    }

    [Fact]
    public async Task AlreadyVerifiedUserGetsIdempotentSuccess()
    {
        var context = TestContextWithUser();
        context.User.IsEmailVerified = true;

        var result = await context.Service.VerifyEmailAsync(
            Verify(context.User.Email, "123456"));

        Assert.True(result.IsEmailVerified);
        Assert.False(result.IsPhoneNumberVerified);
    }

    [Fact]
    public async Task ResendDoesNotRevealWhetherEmailExists()
    {
        var context = TestContextWithUser();

        var existing = await context.Service.ResendEmailVerificationAsync(
            Resend(context.User.Email));
        var missing = await context.Service.ResendEmailVerificationAsync(
            Resend("missing@test.local"));

        Assert.Equal(existing.Message, missing.Message);
        Assert.InRange(missing.ResendCooldownSeconds, 59, 60);
    }

    [Fact]
    public async Task RegistrationReportsProviderFailureAndConsumesUndeliveredOtp()
    {
        var context = TestContext(emailThrows: true);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            context.Service.RegisterAsync(Registration()));

        Assert.Single(((TestUserRepository)context.UnitOfWork.Users).Items);
        Assert.NotNull(context.UnitOfWork.VerificationCodeRepository.Items.Single().ConsumedAt);
    }

    [Fact]
    public async Task PasswordResetCodeCannotVerifyAccount()
    {
        var context = TestContextWithUser();
        ((TestPasswordResetCodeRepository)context.UnitOfWork.PasswordResetCodes).Items.Add(
            new PasswordResetCode
            {
                UserId = context.User.Id,
                CodeHash = "not-an-account-verification-code",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            });

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.VerifyEmailAsync(Verify(context.User.Email, "123456")));
    }

    [Fact]
    public async Task StaffInvitationCodeCannotVerifyAccount()
    {
        var context = TestContextWithUser();
        ((TestPasswordResetCodeRepository)context.UnitOfWork.PasswordResetCodes).Items.Add(
            new PasswordResetCode
            {
                UserId = context.User.Id,
                CodeHash = "staff-invitation-code-hash",
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.VerifyEmailAsync(Verify(context.User.Email, "654321")));
    }

    private static EmailVerificationTestContext TestContextWithUser()
    {
        var context = TestContext();
        context.User = new User
        {
            Email = "verify@test.local",
            PhoneNumber = "08000000000",
            PasswordHash = "hash",
            Status = UserStatus.Active,
            IsEmailVerified = false,
            IsPhoneNumberVerified = false
        };
        context.User.PasswordHash = new PasswordHasher().Hash("Password123!");
        ((TestUserRepository)context.UnitOfWork.Users).Items.Add(context.User);
        return context;
    }

    private static EmailVerificationTestContext TestContext(bool emailThrows = false)
    {
        var unitOfWork = new TestUnitOfWork();
        var email = new CapturingEmailService(emailThrows);
        var jwt = new FakeJwtService();
        var service = new AuthService(
            unitOfWork,
            new PasswordHasher(),
            jwt,
            email,
            new WalletService(unitOfWork),
            NullLogger<AuthService>.Instance,
            Options.Create(new EmailSettings
            {
                ApiKey = "test-key",
                FromAddress = "test@pryde.local",
                FromName = "Pryde",
                OtpExpiryMinutes = 10
            }),
            new OnboardingStatusService(unitOfWork));
        return new EmailVerificationTestContext(unitOfWork, service, email, jwt);
    }

    private static RegisterRequestDto Registration() => new()
    {
        Email = "new-user@test.local",
        PhoneNumber = "08000000001",
        Password = "Password123!",
        FirstName = "New",
        LastName = "User"
    };

    private static LoginRequestDto Login(string email) => new()
    {
        EmailOrPhone = email,
        Password = "Password123!"
    };

    private static EmailVerificationResendRequestDto Resend(string email) =>
        new() { Email = email };

    private static EmailVerificationVerifyRequestDto Verify(string email, string code) =>
        new() { Email = email, Code = code };

    private sealed class EmailVerificationTestContext(
        TestUnitOfWork unitOfWork,
        AuthService service,
        CapturingEmailService email,
        FakeJwtService jwt)
    {
        public TestUnitOfWork UnitOfWork { get; } = unitOfWork;
        public AuthService Service { get; } = service;
        public CapturingEmailService Email { get; } = email;
        public FakeJwtService Jwt { get; } = jwt;
        public User User { get; set; } = null!;
    }

    private sealed class CapturingEmailService(bool throws) : IEmailService
    {
        public List<SentEmail> Messages { get; } = [];

        public Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            if (throws) throw new HttpRequestException("Provider unavailable.");
            var match = Regex.Match(htmlBody, @"\b\d{6}\b");
            Messages.Add(new SentEmail(toEmail, subject, htmlBody, match.Value));
            return Task.CompletedTask;
        }
    }

    private sealed record SentEmail(
        string ToEmail,
        string Subject,
        string HtmlBody,
        string Code);

    private sealed class FakeJwtService : IJwtService
    {
        public int AccessTokensGenerated { get; private set; }
        public IReadOnlyList<string> LastRoles { get; private set; } = [];
        public int RefreshTokenExpiryDays => 30;
        public string GenerateAccessToken(
            Guid userId, string email, IEnumerable<string> roles)
        {
            AccessTokensGenerated++;
            LastRoles = roles.ToList();
            return "access-token";
        }
        public string GenerateRefreshToken() => Guid.NewGuid().ToString("N");
        public string HashToken(string token) => token;
    }
}
