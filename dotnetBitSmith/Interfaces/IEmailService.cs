using System.Threading.Tasks;

namespace dotnetBitSmith.Interfaces {
    public interface IEmailService {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }
}
