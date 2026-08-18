using System.Text.Encodings.Web;

namespace Pryde.Services.Notifications;

internal static class PrydeEmailTemplates
{
    public static string EmailVerificationSucceeded(string? firstName) =>
        Build(
            firstName,
            "Your email is verified",
            "Your email address has been successfully verified.",
            "Your Pryde account is now ready for the next stage of setup." +
            " You can continue completing your profile and any required verification " +
            "so you can access the features available to you on the platform.");

    public static string KycApproved(string? firstName) =>
        Build(
            firstName,
            "Identity verification successful",
            "Your identity verification has been successfully completed and approved.",
            "This helps us keep Pryde safe and ensures that people using the platform are properly verified." +
            " You can now continue setting up your account and access the features available to you on Pryde.");

    public static string KycRejected(
        string? firstName,
        string? rejectionReason) =>
        Build(
            firstName,
            "Identity verification unsuccessful",
            "We were unable to approve your identity verification.",
            BuildKycRejectedNextStep(rejectionReason));

    public static string DriverOnboardingApproved(string? firstName) =>
        Build(
            firstName,
            "Driver onboarding approved",
            "Your driver onboarding has been approved, and your driver account is now active on Pryde.",
            "You can now create trips, offer available seats to passengers travelling along your route," +
            " manage booking requests, and manage your trips from the Pryde app.");

    public static string DriverAccountDeactivated(string? firstName) =>
        Build(
            firstName,
            "Driver account deactivated",
            "Your driver account has been deactivated on Pryde.",
            "You will not be able to use driver features such as creating trips" +
            " or accepting passenger bookings while your driver account is deactivated." +
            " Please contact Pryde support if you need more information about this decision " +
            "or the next steps available to you.");

    public static string DriverOnboardingRejected(
        string? firstName,
        string rejectionReason) =>
        Build(
            firstName,
            "Driver onboarding requires attention",
            "Your driver onboarding application was not approved.",
            BuildDriverRejectionNextStep(rejectionReason));

    private static string BuildKycRejectedNextStep(string? rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            return "Please review the information and documents you submitted. " +
                "If the verification flow allows another attempt, correct any issues and complete " +
                "the verification again so we can continue reviewing your Pryde account.";
        }

        return
            $"Reason: {rejectionReason.Trim()} " +
            "Please review the information or document involved and, " +
            "if another verification attempt is available, correct the issue before trying again.";
    }

    private static string BuildDriverRejectionNextStep(string rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            return "Please review your driver onboarding information and any documents you submitted." +
                " You can correct the required information and continue the onboarding process when another attempt is available.";
        }

        return
            $"Reason: {rejectionReason.Trim()} " +
            "Please review the issue above and correct the relevant onboarding information or document before trying again.";
    }

    private static string Build(
        string? firstName,
        string heading,
        string message,
        string nextStep)
    {
        var safeFirstName = string.IsNullOrWhiteSpace(firstName)
            ? null
            : HtmlEncoder.Default.Encode(firstName.Trim());

        var safeHeading = HtmlEncoder.Default.Encode(heading);
        var safeMessage = HtmlEncoder.Default.Encode(message);
        var safeNextStep = HtmlEncoder.Default.Encode(nextStep);

        var greeting = safeFirstName is null
            ? "Hello,"
            : $"Hello {safeFirstName},";

        return $"""
            <div style="margin:0; padding:24px; background-color:#f5f7fa; font-family:Arial, Helvetica, sans-serif; color:#1f2937;">
                <div style="max-width:600px; margin:0 auto; background-color:#ffffff; border:1px solid #e5e7eb; border-radius:12px; overflow:hidden;">
                    <div style="padding:28px 32px; text-align:center; background-color:#111827;">
                        <h1 style="margin:0; color:#ffffff; font-size:28px;">Pryde</h1>
                    </div>

                    <div style="padding:32px;">
                        <p style="margin:0 0 20px; font-size:16px; line-height:1.6;">
                            {greeting}
                        </p>

                        <h2 style="margin:0 0 16px; font-size:22px; color:#111827;">
                            {safeHeading}
                        </h2>

                        <p style="margin:0 0 20px; font-size:16px; line-height:1.6;">
                            {safeMessage}
                        </p>

                        <p style="margin:0 0 24px; font-size:15px; line-height:1.6; color:#4b5563;">
                            {safeNextStep}
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
    }
}