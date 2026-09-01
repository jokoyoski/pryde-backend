using System.Text.Encodings.Web;

namespace Pryde.Services.Notifications;

internal static class PrydeEmailTemplates
{
    internal const string LogoUrl = "https://res.cloudinary.com/pinterest-site/image/upload/f_jpg,q_auto:good/v1788134947/white-pryde_yxr0ej.jpg";

    public static string EmailVerificationOtp(
        string? firstName,
        string code,
        int otpExpiryMinutes)
    {
        var safeCode = Encode(code);
        return BuildLayout(
            Greeting(firstName),
            $"""
            <h2 style="margin:0 0 16px; font-size:22px; color:#111827;">
                Welcome to Pryde.
            </h2>

            <p style="margin:0 0 20px; font-size:16px; line-height:1.6;">
                Pryde connects drivers with available seats to passengers travelling
                along the same route, making everyday journeys more convenient and affordable.
            </p>

            <p style="margin:0 0 24px; font-size:16px; line-height:1.6;">
                Use the verification code below to confirm your email address
                and continue setting up your account:
            </p>

            {OtpCode(safeCode)}

            <p style="margin:0 0 18px; font-size:15px; line-height:1.6; color:#4b5563;">
                This code will expire in
                <strong>{otpExpiryMinutes} minutes</strong>.
            </p>

            <p style="margin:0 0 18px; font-size:15px; line-height:1.6; color:#4b5563;">
                For your security, do not share this code with anyone.
                Pryde representatives will never ask you to provide your verification code.
            </p>

            <p style="margin:0 0 24px; font-size:15px; line-height:1.6; color:#4b5563;">
                If you did not create a Pryde account, you can safely ignore this email.
            </p>
            """);
    }

    public static string EmailVerificationSucceeded(string? firstName) =>
        BuildMessage(
            firstName,
            "Your email is verified",
            "Your email address has been successfully verified.",
            "Your Pryde account is now ready for the next stage of setup." +
            " You can continue completing your profile and any required verification " +
            "so you can access the features available to you on the platform.");

    public static string PasswordResetOtp(string? firstName, string code)
    {
        var safeCode = Encode(code);
        return BuildLayout(
            Greeting(firstName),
            $"""
            <h2 style="margin:0 0 16px; font-size:22px; color:#111827;">
                Reset your Pryde password
            </h2>

            <p style="margin:0 0 20px; font-size:16px; line-height:1.6;">
                We received a request to reset the password for your Pryde account.
            </p>

            <p style="margin:0 0 24px; font-size:16px; line-height:1.6;">
                Use the verification code below to continue:
            </p>

            {OtpCode(safeCode)}

            <p style="margin:0 0 18px; font-size:15px; line-height:1.6; color:#4b5563;">
                This code will expire in
                <strong>10 minutes</strong>.
            </p>

            <p style="margin:0 0 18px; font-size:15px; line-height:1.6; color:#4b5563;">
                For your security, do not share this code with anyone.
                Pryde representatives will never ask you to provide your password reset code.
            </p>

            <p style="margin:0 0 24px; font-size:15px; line-height:1.6; color:#4b5563;">
                If you did not request a password reset, you can safely ignore this email.
            </p>
            """);
    }

    public static string StaffInvitation(
        string? firstName,
        string roleName,
        string code)
    {
        var safeRoleName = Encode(roleName);
        var safeCode = Encode(code);
        return BuildLayout(
            Greeting(firstName),
            $"""
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

            {OtpCode(safeCode)}

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
            """);
    }

    public static string WithdrawalOtp(string code, int otpExpiryMinutes)
    {
        var safeCode = Encode(code);
        return BuildLayout(
            "Hello,",
            $"""
            <h2 style="margin:0 0 16px; font-size:22px; color:#111827;">
                Confirm your withdrawal
            </h2>

            <p style="margin:0 0 24px; font-size:16px; line-height:1.6;">
                Use the verification code below to confirm your Pryde wallet withdrawal:
            </p>

            {OtpCode(safeCode)}

            <p style="margin:0 0 18px; font-size:15px; line-height:1.6; color:#4b5563;">
                This code will expire in
                <strong>{otpExpiryMinutes} minutes</strong>.
            </p>

            <p style="margin:0 0 18px; font-size:15px; line-height:1.6; color:#4b5563;">
                For your security, do not share this code with anyone.
                Pryde representatives will never ask you to provide your withdrawal verification code.
            </p>

            <p style="margin:0 0 24px; font-size:15px; line-height:1.6; color:#4b5563;">
                If you did not request this withdrawal, do not use this code and review your account activity.
            </p>
            """);
    }

    public static string KycApproved(string? firstName) =>
        BuildMessage(
            firstName,
            "Identity verification successful",
            "Your identity verification has been successfully completed and approved.",
            "This helps us keep Pryde safe and ensures that people using the platform are properly verified." +
            " You can now continue setting up your account and access the features available to you on Pryde.");

    public static string KycRejected(
        string? firstName,
        string? rejectionReason) =>
        BuildMessage(
            firstName,
            "Identity verification unsuccessful",
            "We were unable to approve your identity verification.",
            BuildKycRejectedNextStep(rejectionReason));

    public static string DriverOnboardingApproved(string? firstName) =>
        BuildMessage(
            firstName,
            "Driver onboarding approved",
            "Your driver onboarding has been approved, and your driver account is now active on Pryde.",
            "You can now create trips, offer available seats to passengers travelling along your route," +
            " manage booking requests, and manage your trips from the Pryde app.");

    public static string DriverAccountDeactivated(string? firstName) =>
        BuildMessage(
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
        BuildMessage(
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

    private static string BuildMessage(
        string? firstName,
        string heading,
        string message,
        string nextStep) =>
        BuildLayout(
            Greeting(firstName),
            $"""
            <h2 style="margin:0 0 16px; font-size:22px; color:#111827;">
                {Encode(heading)}
            </h2>

            <p style="margin:0 0 20px; font-size:16px; line-height:1.6;">
                {Encode(message)}
            </p>

            <p style="margin:0 0 24px; font-size:15px; line-height:1.6; color:#4b5563;">
                {Encode(nextStep)}
            </p>
            """);

    private static string BuildLayout(string greeting, string content) =>
        $"""
           <div style="margin:0; padding:24px; background-color:#f5f7fa; font-family:Arial, Helvetica, sans-serif; color:#1f2937;">
            <div style="max-width:600px; margin:0 auto; background-color:#ffffff; border:1px solid #e5e7eb; border-radius:12px; overflow:hidden;">
                <div style="padding:28px 32px; text-align:center; background-color:#111827;">
            <img src="{LogoUrl}" alt="Pryde" width="140" style="display:block; width:140px; height:auto; margin:0 auto; border:0;" />
            </div>

            <div style="padding:32px;">
                    <p style="margin:0 0 20px; font-size:16px; line-height:1.6;">
                        {greeting}
                    </p>

                    {content}

                    <p style="margin:0; font-size:15px; line-height:1.6;">
                        Regards,<br />
                        <strong>The Pryde Team</strong>
                    </p>
                </div>

                <div style="padding:20px 32px; text-align:center; background-color:#f9fafb; border-top:1px solid #e5e7eb;">
                    <p style="margin:0; font-size:13px; color:#6b7280;">
                        &copy; {DateTime.UtcNow.Year} Pryde. All rights reserved.
                    </p>
                </div>
            </div>
        </div>
        """;

    private static string Greeting(string? firstName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            return "Hello,";

        return $"Hello {Encode(firstName.Trim())},";
    }

    private static string OtpCode(string safeCode) =>
        $"""
        <div style="margin:24px 0; padding:22px; text-align:center; background-color:#f3f4f6; border-radius:8px;">
            <span style="font-size:32px; font-weight:700; letter-spacing:8px; color:#111827;">
                {safeCode}
            </span>
        </div>
        """;

    private static string Encode(string value) =>
        HtmlEncoder.Default.Encode(value);
}
