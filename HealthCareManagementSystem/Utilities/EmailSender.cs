using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace HealthCareManagementSystem.Utilities
{
    public class EmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;

        public EmailSender(IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(_settings.UserName) || string.IsNullOrWhiteSpace(_settings.Password))
            {
                throw new InvalidOperationException(
                    "Email is not configured. Set Email:UserName and Email:Password using a secure configuration provider.");
            }

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_settings.UserName, _settings.Password)
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(string.IsNullOrWhiteSpace(_settings.From) ? _settings.UserName : _settings.From),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            mail.To.Add(email);

            await client.SendMailAsync(mail);
        }
    }
}
