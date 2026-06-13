using dotnetBitSmith.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace dotnetBitSmith.Services {
    public class SmtpEmailService : IEmailService {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger) {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body) {
            var smtpHost = _configuration["SmtpSettings:Host"];
            var smtpPortVal = _configuration["SmtpSettings:Port"];
            var smtpUser = _configuration["SmtpSettings:Username"];
            var smtpPass = _configuration["SmtpSettings:Password"];
            var smtpFrom = _configuration["SmtpSettings:FromEmail"] ?? "no-reply@compylr.com";

            if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass)) {
                _logger.LogWarning("SMTP Settings not fully configured in appsettings.json. Email NOT sent via SMTP.");
                var emailLog = $"\n========================================\n" +
                               $"EMAIL TO: {toEmail}\n" +
                               $"SUBJECT: {subject}\n" +
                               $"BODY:\n{body}\n" +
                               $"========================================\n";
                _logger.LogInformation(emailLog);
                Console.WriteLine(emailLog);
                return;
            }

            int smtpPort = 587;
            if (!string.IsNullOrWhiteSpace(smtpPortVal)) {
                int.TryParse(smtpPortVal, out smtpPort);
            }

            try {
                using (var message = new MailMessage()) {
                    message.From = new MailAddress(smtpFrom, "Compylr");
                    message.To.Add(new MailAddress(toEmail));
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = true;

                    using (var client = new SmtpClient(smtpHost, smtpPort)) {
                        client.Credentials = new NetworkCredential(smtpUser, smtpPass);
                        client.EnableSsl = true;
                        
                        var sendTask = client.SendMailAsync(message);
                        var timeoutTask = Task.Delay(5000);
                        var completedTask = await Task.WhenAny(sendTask, timeoutTask);
                        if (completedTask == timeoutTask) {
                            throw new TimeoutException("SMTP connection attempt timed out.");
                        }
                        await sendTask; // propagate any exceptions
                    }
                }
                _logger.LogInformation("Successfully sent email to {ToEmail}", toEmail);
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to send email to {ToEmail} via SMTP.", toEmail);
                throw;
            }
        }
    }
}
