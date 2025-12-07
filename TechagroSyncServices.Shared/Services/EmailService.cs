using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Settings;

namespace TechagroSyncServices.Shared.Services
{
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _smtpSettings;

        public EmailService(SmtpSettings smtpSettings)
        {
            _smtpSettings = smtpSettings;
        }

        public async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Intercars Sync Service", _smtpSettings.User));

            var recipients = to.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var address in recipients)
            {
                message.To.Add(MailboxAddress.Parse(address.Trim()));
            }

            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync(_smtpSettings.Host, _smtpSettings.Port, SecureSocketOptions.SslOnConnect);
                await client.AuthenticateAsync(_smtpSettings.User, _smtpSettings.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
        }
    }
}