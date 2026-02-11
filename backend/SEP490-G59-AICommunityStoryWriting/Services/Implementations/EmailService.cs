using Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Services.Implementations
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var host = Get("SMTP_HOST", "Email:SmtpHost");
            var portRaw = Get("SMTP_PORT", "Email:SmtpPort");
            var securityRaw = Get("SMTP_SECURITY", "Email:Security");
            var username = Get("SMTP_USERNAME", "Email:Username");
            var password = Get("SMTP_PASSWORD", "Email:Password");
            var fromEmail = Get("FROM_EMAIL", "Email:FromEmail") ?? username;
            var fromName = Get("FROM_NAME", "Email:FromName") ?? "AICommunityStoryWriting";

            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("Missing SMTP host (SMTP_HOST / Email:SmtpHost).");
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("Missing SMTP username (SMTP_USERNAME / Email:Username).");
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Missing SMTP password (SMTP_PASSWORD / Email:Password).");

            // Gmail app-passwords are often written with spaces; remove them safely.
            password = password.Replace(" ", "");

            var port = 587;
            if (!string.IsNullOrWhiteSpace(portRaw) && int.TryParse(portRaw, out var p) && p > 0)
                port = p;

            var secureOption = ParseSecureSocketOptions(securityRaw, port);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject ?? "";

            var isHtml = IsHtmlBody(body) || IsConfigTrue(Get("EMAIL_IS_HTML", "Email:IsHtml"));
            message.Body = new BodyBuilder
            {
                HtmlBody = isHtml ? body : null,
                TextBody = isHtml ? StripToText(body) : body
            }.ToMessageBody();

            using var client = new SmtpClient();
            // Optional: reduce protocol noise
            client.Timeout = 30_000;

            await client.ConnectAsync(host, port, secureOption);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        private string? Get(string envKey, string configKey)
        {
            return _config[envKey] ?? _config[configKey];
        }

        private static SecureSocketOptions ParseSecureSocketOptions(string? securityRaw, int port)
        {
            var s = (securityRaw ?? "").Trim().ToUpperInvariant();
            return s switch
            {
                "STARTTLS" => SecureSocketOptions.StartTls,
                "SSL" => SecureSocketOptions.SslOnConnect,
                "TLS" => SecureSocketOptions.SslOnConnect,
                _ => port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls
            };
        }

        private static bool IsConfigTrue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Trim().Equals("1");
        }

        private static bool IsHtmlBody(string body)
        {
            if (string.IsNullOrEmpty(body)) return false;
            // naive but effective for OTP emails
            return body.Contains("<") && body.Contains(">");
        }

        private static string StripToText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            // Minimal fallback; not a full HTML sanitizer.
            return html
                .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
                .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
                .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
                .Replace("<b>", "", StringComparison.OrdinalIgnoreCase)
                .Replace("</b>", "", StringComparison.OrdinalIgnoreCase)
                .Replace("<strong>", "", StringComparison.OrdinalIgnoreCase)
                .Replace("</strong>", "", StringComparison.OrdinalIgnoreCase);
        }
    }
}
