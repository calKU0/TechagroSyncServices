using System.Threading.Tasks;

namespace TechagroSyncServices.Shared.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string htmlBody);
    }
}