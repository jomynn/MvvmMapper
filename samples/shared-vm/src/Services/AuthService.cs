using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SharedVmApp.Services;

public sealed class AuthService : IAuthService
{
    private readonly HttpClient _http;

    public AuthService(HttpClient http) => _http = http;

    public async Task LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        await _http.PostAsync("/api/auth/login", null!, cancellationToken);
    }

    public async Task RegisterAsync(string username, string email, string password, CancellationToken cancellationToken = default)
    {
        await _http.PostAsync("/api/auth/register", null!, cancellationToken);
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        await _http.PostAsync("/api/auth/forgot-password", null!, cancellationToken);
    }
}
