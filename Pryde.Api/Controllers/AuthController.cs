using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Pryde.Contracts.RequestModels;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);

        return Ok(result);
    }

    [HttpPost("roles/select")]
    [Authorize]
    public async Task<IActionResult> SelectRoles(
        [FromBody] SelectRolesRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await authService.SelectRolesAsync(
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            request,
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await authService.RefreshTokenAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequestDto request,
        CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request, cancellationToken);
        return Ok(new { message = "Logged out successfully." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await authService.ForgotPasswordAsync(request, cancellationToken);
        return Ok(new { message = "If that email exists, a reset code has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await authService.ResetPasswordAsync(request, cancellationToken);
        return Ok(new { message = "Password reset successfully." });
    }
    [HttpPost("email-verification/resend")]
    public async Task<IActionResult> ResendEmailVerification(
        [FromBody] EmailVerificationResendRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await authService.ResendEmailVerificationAsync(
            request, cancellationToken));
    }

    [HttpPost("email-verification/verify")]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] EmailVerificationVerifyRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await authService.VerifyEmailAsync(request, cancellationToken));
    }

    [HttpGet("verification-status")]
    [Authorize]
    public async Task<IActionResult> GetVerificationStatus(
        CancellationToken cancellationToken)
    {
        return Ok(await authService.GetVerificationStatusAsync(
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            cancellationToken));
    }
}
