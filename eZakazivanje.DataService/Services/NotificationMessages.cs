using System.Globalization;

namespace eZakazivanje.DataService.Services;

/// <summary>
/// Localized strings for Firebase push notifications, appointment reminder emails, and other transactional emails. Default language is English.
/// </summary>
public static class NotificationMessages
{
    public const string DefaultLanguage = "en";

    /// <summary>Returns a supported language code: "en" or "sr". Unknown values fall back to English.</summary>
    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return DefaultLanguage;

        var l = language.Trim().ToLowerInvariant();
        if (l.StartsWith("sr", StringComparison.Ordinal))
            return "sr";
        if (l.StartsWith("en", StringComparison.Ordinal))
            return "en";
        return DefaultLanguage;
    }

    public static (string Title, string Body) AppointmentBookedForBusiness(
        string? language,
        string serviceName,
        string userFullName,
        DateTime appointmentDate,
        TimeSpan startTime)
    {
        var lang = NormalizeLanguage(language);
        var dateStr = appointmentDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var timeStr = string.Format(CultureInfo.InvariantCulture, "{0:hh\\:mm}", startTime);

        if (lang == "sr")
        {
            return (
                serviceName,
                $"{userFullName} je zakazao/la termin za {dateStr} u {timeStr}.");
        }

        return (
            serviceName,
            $"{userFullName} has booked an appointment for {dateStr} at {timeStr}.");
    }

    public static (string Title, string Body) AppointmentCancelledForCustomer(
        string? language,
        string businessName,
        DateTime appointmentDate,
        TimeSpan startTime)
    {
        var lang = NormalizeLanguage(language);
        var dateStr = appointmentDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var timeStr = string.Format(CultureInfo.InvariantCulture, "{0:hh\\:mm}", startTime);

        if (lang == "sr")
        {
            return (
                businessName,
                $"Vaš termin za {dateStr} u {timeStr} je otkazan od strane salona.");
        }

        return (
            businessName,
            $"Your appointment for {dateStr} at {timeStr} has been cancelled by the business.");
    }

    public static (string Title, string Body) AppointmentReminder(
        string? language,
        string businessName,
        string timeFrameKey)
    {
        var lang = NormalizeLanguage(language);
        var title = businessName;

        if (lang == "sr")
        {
            var body = timeFrameKey switch
            {
                "24 sata" => "Imate zakazan termin sutra.",
                "2 sata" => "Vaš zakazani termin počinje za 2 sata.",
                _ => $"Vaš zakazani termin počinje za {timeFrameKey}."
            };
            return (title, body);
        }

        var bodyEn = timeFrameKey switch
        {
            "24 sata" => "You have an appointment scheduled for tomorrow.",
            "2 sata" => "Your scheduled appointment starts in 2 hours.",
            _ => $"Your scheduled appointment starts in {timeFrameKey}."
        };
        return (title, bodyEn);
    }

    /// <summary>Human-readable duration for reminder emails (English / Serbian).</summary>
    public static string FormatAppointmentDuration(string? language, TimeSpan duration)
    {
        var lang = NormalizeLanguage(language);

        if (duration.TotalHours >= 1)
        {
            var hours = (int)duration.TotalHours;
            var minutes = duration.Minutes;

            if (minutes > 0)
            {
                if (lang == "sr")
                    return hours == 1
                        ? $"1 sat i {minutes} minuta"
                        : $"{hours} sati i {minutes} minuta";

                return hours == 1
                    ? $"1 Hour and {minutes} Minutes"
                    : $"{hours} Hours and {minutes} Minutes";
            }

            if (lang == "sr")
                return hours == 1 ? "1 sat" : $"{hours} sati";

            return hours == 1 ? "1 Hour" : $"{hours} Hours";
        }

        return lang == "sr"
            ? $"{duration.Minutes} minuta"
            : $"{duration.Minutes} Minutes";
    }

    /// <summary>Subject and plain-text body for appointment reminder emails.</summary>
    public static (string Subject, string Body) AppointmentReminderEmail(
        string? language,
        string? firstName,
        string businessName,
        string timeFrameKey,
        string durationText)
    {
        var lang = NormalizeLanguage(language);
        var greetingName = string.IsNullOrWhiteSpace(firstName) ? null : firstName.Trim();

        if (lang == "sr")
        {
            var greeting = greetingName == null ? "Zdravo," : $"Zdravo {greetingName},";
            var subject = "Podsetnik za termin";

            string bodyMain = timeFrameKey switch
            {
                "24 sata" =>
                    $"Ovo je podsetnik za vaš termin zakazan za sutra kod {businessName}.\n" +
                    $"Trajanje termina: {durationText}.",
                "2 sata" =>
                    $"Ovo je podsetnik da imate termin kod {businessName} koji počinje za 2 sata.\n" +
                    $"Trajanje termina: {durationText}.",
                _ =>
                    $"Ovo je podsetnik da imate termin kod {businessName} koji počinje za {timeFrameKey}.\n" +
                    $"Trajanje termina: {durationText}."
            };

            var body = $"{greeting}\n\n{bodyMain}\n\nSrdačan pozdrav,\nTermino";
            return (subject, body);
        }

        var greetingEn = greetingName == null ? "Hello," : $"Hello {greetingName},";
        var subjectEn = "Appointment Reminder";

        string bodyMainEn = timeFrameKey switch
        {
            "24 sata" =>
                $"This is a reminder about your appointment scheduled for tomorrow at {businessName}.\n" +
                $"Your appointment duration: {durationText}.",
            "2 sata" =>
                $"This is a reminder that you have an appointment at {businessName} starting in 2 hours.\n" +
                $"Your appointment duration: {durationText}.",
            _ =>
                $"This is a reminder that you have an appointment at {businessName} starting in {timeFrameKey}.\n" +
                $"Your appointment duration: {durationText}."
        };

        var bodyEn = $"{greetingEn}\n\n{bodyMainEn}\n\nBest regards,\nTermino";
        return (subjectEn, bodyEn);
    }

    private static string EmailGreeting(string? language, string? firstName)
    {
        var lang = NormalizeLanguage(language);
        var name = string.IsNullOrWhiteSpace(firstName) ? null : firstName.Trim();
        if (lang == "sr")
            return name == null ? "Zdravo," : $"Zdravo {name},";
        return name == null ? "Hello," : $"Hello {name},";
    }

    private static string EmailClosing(string? language) =>
        NormalizeLanguage(language) == "sr"
            ? "Srdačan pozdrav,\nTermino"
            : "Best regards,\nTermino";

    /// <param name="registrationContext">True after signup; false for resend / manual send flows.</param>
    public static (string Subject, string Body) EmailVerification(
        string? language,
        string? firstName,
        string verificationLink,
        bool registrationContext)
    {
        var lang = NormalizeLanguage(language);
        var greeting = EmailGreeting(language, firstName);

        if (lang == "sr")
        {
            var subject = "Verifikacija email adrese";
            var ignoreLine = registrationContext
                ? "Ako niste tražili ovu registraciju, ignorišite ovaj email."
                : "Ako niste zatražili verifikaciju, ignorišite ovaj email.";
            var body =
                $"{greeting}\n\n" +
                "Molimo potvrdite svoju email adresu klikom na link ispod:\n\n" +
                $"{verificationLink}\n\n" +
                $"{ignoreLine}\n\n" +
                EmailClosing(language);
            return (subject, body);
        }

        var subjectEn = "Email Verification";
        var ignoreEn = registrationContext
            ? "If you did not request this registration, please ignore this email."
            : "If you did not request verification, please ignore this email.";
        var introEn = registrationContext
            ? "Please confirm your email address by clicking on the link below:\n\n"
            : "Please verify your email address by clicking on the link below:\n\n";
        var bodyEn =
            $"{greeting}\n\n" +
            introEn +
            $"{verificationLink}\n\n" +
            $"{ignoreEn}\n\n" +
            EmailClosing(language);
        return (subjectEn, bodyEn);
    }

    public static (string Subject, string Body) PasswordResetEmail(
        string? language,
        string? firstName,
        string resetLink)
    {
        var lang = NormalizeLanguage(language);
        var greeting = EmailGreeting(language, firstName);

        if (lang == "sr")
        {
            var subject = "Zahtev za resetovanje lozinke";
            var body =
                $"{greeting}\n\n" +
                "Zatražili ste resetovanje lozinke. Kliknite na link ispod da postavite novu lozinku:\n\n" +
                $"{resetLink}\n\n" +
                "Ako niste tražili resetovanje lozinke, ignorišite ovaj email.\n\n" +
                EmailClosing(language);
            return (subject, body);
        }

        var subjectEn = "Password Reset Request";
        var bodyEn =
            $"{greeting}\n\n" +
            "You have requested to reset your password. Please click on the link below to reset it:\n\n" +
            $"{resetLink}\n\n" +
            "If you did not request a password reset, please ignore this email.\n\n" +
            EmailClosing(language);
        return (subjectEn, bodyEn);
    }

    public static (string Subject, string Body) BusinessApprovedEmail(
        string? language,
        string? firstName,
        string businessName,
        string verificationLink)
    {
        var lang = NormalizeLanguage(language);
        var greeting = EmailGreeting(language, firstName);

        if (lang == "sr")
        {
            var subject = "Vaš biznis je odobren";
            var body =
                $"{greeting}\n\n" +
                $"Vaš zahtev za registraciju biznisa „{businessName}“ je odobren. Molimo potvrdite svoju email adresu klikom na link ispod:\n\n" +
                $"{verificationLink}\n\n" +
                EmailClosing(language);
            return (subject, body);
        }

        var subjectEn = "Your Business Has Been Approved";
        var bodyEn =
            $"{greeting}\n\n" +
            $"Your business '{businessName}' registration request has been approved. Please confirm your email address by clicking on the link below:\n\n" +
            $"{verificationLink}\n\n" +
            EmailClosing(language);
        return (subjectEn, bodyEn);
    }

    public static (string Subject, string Body) BusinessRegistrationRejectedEmail(
        string? language,
        string? firstName)
    {
        var lang = NormalizeLanguage(language);
        var greeting = EmailGreeting(language, firstName);

        if (lang == "sr")
        {
            var subject = "Zahtev za registraciju biznisa je odbijen";
            var body =
                $"{greeting}\n\n" +
                "Nažalost, vaš zahtev za registraciju biznisa je odbijen.\n\n" +
                "Ako imate pitanja, kontaktirajte našu podršku.\n\n" +
                EmailClosing(language);
            return (subject, body);
        }

        var subjectEn = "Your Business Registration Request Has Been Rejected";
        var bodyEn =
            $"{greeting}\n\n" +
            "Unfortunately, your business registration request has been rejected.\n\n" +
            "If you have any questions, please contact our support.\n\n" +
            EmailClosing(language);
        return (subjectEn, bodyEn);
    }

    /// <summary>Admin notification: English and Serbian in one message (not tied to end-user preferred language).</summary>
    public static (string Subject, string Body) AdminNewBusinessRegistrationEmail(
        string phoneNumber,
        string businessName,
        string businessDescription,
        string businessEmail)
    {
        var subject = "New business registration request / Novi zahtev za registraciju biznisa";
        var body =
            "--- English ---\n" +
            "A new business registration request was submitted.\n\n" +
            $"Business name: {businessName}\n" +
            $"Description: {businessDescription}\n" +
            $"Business email: {businessEmail}\n" +
            $"Phone: {phoneNumber}\n\n" +
            "Please sign in to the admin panel to review and approve or reject the request.\n\n" +
            "--- Srpski ---\n" +
            "Novi zahtev za registraciju biznisa:\n\n" +
            $"Naziv biznisa: {businessName}\n" +
            $"Opis biznisa: {businessDescription}\n" +
            $"Email biznisa: {businessEmail}\n" +
            $"Broj telefona: {phoneNumber}\n\n" +
            "Molimo prijavite se na admin panel da biste pregledali i odobrili/odbili zahtev.\n\n" +
            "Termino";
        return (subject, body);
    }

    public static (string Subject, string Body) BusinessActivationLinkEmail(
        string? language,
        string businessName,
        string approvalLink)
    {
        var lang = NormalizeLanguage(language);

        if (lang == "sr")
        {
            var subject = "Aktivirajte svoj biznis";
            var body =
                "Zdravo,\n\n" +
                $"Vaš biznis „{businessName}“ je kreiran i čeka aktivaciju.\n\n" +
                "Aktivirajte ga klikom na link ispod:\n\n" +
                $"{approvalLink}\n\n" +
                "Ako niste tražili ovo, ignorišite ovaj email.\n\n" +
                EmailClosing(language);
            return (subject, body);
        }

        var subjectEn = "Activate your business";
        var bodyEn =
            "Hello,\n\n" +
            $"Your business \"{businessName}\" was created and is pending activation.\n\n" +
            "Please activate it by clicking the link below:\n\n" +
            $"{approvalLink}\n\n" +
            "If you didn't request this, you can ignore this email.\n\n" +
            EmailClosing(language);
        return (subjectEn, bodyEn);
    }
}
