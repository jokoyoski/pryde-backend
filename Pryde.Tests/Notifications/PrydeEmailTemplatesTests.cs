using System.Reflection;
using System.Text.Encodings.Web;
using Pryde.Services.Service.Implementation;

namespace Pryde.Tests.Notifications;

public class PrydeEmailTemplatesTests
{
    private const string LogoUrl =
        "https://res.cloudinary.com/pinterest-site/image/upload/v1788134947/white-pryde_yxr0ej.jpg";

    private static readonly Type Templates = typeof(AuthService).Assembly.GetType(
        "Pryde.Services.Notifications.PrydeEmailTemplates",
        throwOnError: true)!;

    [Fact]
    public void EmailVerificationOtpUsesSharedLayoutAndEncodesDynamicValues()
    {
        var html = Render(
            "EmailVerificationOtp",
            "<Ada & Co>",
            "<123&>",
            17);

        AssertTemplate(
            html,
            "Welcome to Pryde.",
            "<Ada & Co>",
            "<123&>");
        Assert.Contains("<strong>17 minutes</strong>", html);
    }

    [Fact]
    public void EmailVerificationSuccessUsesSharedLayoutAndEncodesName()
    {
        var html = Render(
            "EmailVerificationSucceeded",
            "<Verified & User>");

        AssertTemplate(
            html,
            "Your email is verified",
            "<Verified & User>");
    }

    [Fact]
    public void PasswordResetOtpUsesSharedLayoutAndEncodesDynamicValues()
    {
        var html = Render(
            "PasswordResetOtp",
            "<Reset & User>",
            "<456&>");

        AssertTemplate(
            html,
            "Reset your Pryde password",
            "<Reset & User>",
            "<456&>");
        Assert.Contains("<strong>10 minutes</strong>", html);
    }

    [Fact]
    public void StaffInvitationUsesSharedLayoutAndEncodesDynamicValues()
    {
        var html = Render(
            "StaffInvitation",
            "<Invited & User>",
            "<Super & Admin>",
            "<789&>");

        AssertTemplate(
            html,
            "You have been invited to join Pryde",
            "<Invited & User>",
            "<Super & Admin>",
            "<789&>");
        Assert.Contains("<strong>24 hours</strong>", html);
    }

    [Fact]
    public void WithdrawalOtpUsesSharedLayoutAndEncodesCode()
    {
        var html = Render(
            "WithdrawalOtp",
            "<012&>",
            10);

        AssertTemplate(
            html,
            "Confirm your withdrawal",
            "<012&>");
        Assert.Contains("<strong>10 minutes</strong>", html);
    }

    [Fact]
    public void KycApprovedUsesSharedLayoutAndEncodesName()
    {
        var html = Render("KycApproved", "<Approved & User>");

        AssertTemplate(
            html,
            "Identity verification successful",
            "<Approved & User>");
    }

    [Fact]
    public void KycRejectedUsesSharedLayoutAndEncodesDynamicValues()
    {
        var html = Render(
            "KycRejected",
            "<Rejected & User>",
            "<Document & mismatch>");

        AssertTemplate(
            html,
            "Identity verification unsuccessful",
            "<Rejected & User>",
            "<Document & mismatch>");
    }

    [Fact]
    public void DriverOnboardingApprovedUsesSharedLayoutAndEncodesName()
    {
        var html = Render(
            "DriverOnboardingApproved",
            "<Approved & Driver>");

        AssertTemplate(
            html,
            "Driver onboarding approved",
            "<Approved & Driver>");
    }

    [Fact]
    public void DriverOnboardingRejectedUsesSharedLayoutAndEncodesDynamicValues()
    {
        var html = Render(
            "DriverOnboardingRejected",
            "<Rejected & Driver>",
            "<Vehicle & document>");

        AssertTemplate(
            html,
            "Driver onboarding requires attention",
            "<Rejected & Driver>",
            "<Vehicle & document>");
    }

    [Fact]
    public void DriverAccountDeactivatedUsesSharedLayoutAndEncodesName()
    {
        var html = Render(
            "DriverAccountDeactivated",
            "<Deactivated & Driver>");

        AssertTemplate(
            html,
            "Driver account deactivated",
            "<Deactivated & Driver>");
    }

    [Fact]
    public void EveryEmailTemplateHasFocusedCoverage()
    {
        var expected = new[]
        {
            "DriverAccountDeactivated",
            "DriverOnboardingApproved",
            "DriverOnboardingRejected",
            "EmailVerificationOtp",
            "EmailVerificationSucceeded",
            "KycApproved",
            "KycRejected",
            "PasswordResetOtp",
            "StaffInvitation",
            "WithdrawalOtp"
        };
        var actual = Templates
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method =>
                method.DeclaringType == Templates &&
                method.ReturnType == typeof(string))
            .Select(method => method.Name)
            .Order()
            .ToArray();

        Assert.Equal(expected, actual);
    }

    private static string Render(string methodName, params object?[] arguments)
    {
        var method = Templates.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(null, arguments));
    }

    private static void AssertTemplate(
        string html,
        string expectedText,
        params string[] dynamicValues)
    {
        Assert.Contains(
            $"<img src=\"{LogoUrl}\" alt=\"Pryde\" width=\"140\" height=\"140\" " +
            "style=\"display:block; max-width:100%; height:auto; margin:0 auto; border:0;\" />",
            html);
        Assert.Contains("background-color:#111827", html);
        Assert.Contains(expectedText, html);
        Assert.Contains("Regards,<br />", html);
        Assert.Contains("The Pryde Team", html);
        Assert.Contains("All rights reserved.", html);

        foreach (var value in dynamicValues)
        {
            Assert.DoesNotContain(value, html);
            Assert.Contains(HtmlEncoder.Default.Encode(value), html);
        }
    }
}
