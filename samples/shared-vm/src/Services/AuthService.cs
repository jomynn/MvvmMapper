using System.Net.Http;
using System.Net.Http.Json;

namespace SharedVmApp.Services;

public sealed class AuthService : IAuthService
{
    private readonly HttpClient _http;

    public AuthService(HttpClient http) => _http = http;

    public async Task LoginAsync(string username, string password)
    {
        await _http.PostAsJsonAsync("/api/auth/login", new { username, password });
    }

    public async Task RegisterAsync(string username, string email, string password)
    {
        await _http.PostAsJsonAsync("/api/auth/register", new { username, email, password });
    }

    public async Task ForgotPasswordAsync(string email)
    {
        await _http.PostAsJsonAsync("/api/auth/forgot-password", new { email });
    }
}
