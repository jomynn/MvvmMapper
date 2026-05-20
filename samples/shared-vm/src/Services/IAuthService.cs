using System.Threading;
using System.Threading.Tasks;

namespace SharedVmApp.Services;

public interface IAuthService
{
    Task LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task RegisterAsync(string username, string email, string password, CancellationToken cancellationToken = default);
    Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
}
