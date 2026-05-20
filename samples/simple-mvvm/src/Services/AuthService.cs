using System.Net.Http;
using System.Net.Http.Json;

namespace SimpleApp.Services;

internal sealed class AuthService : IAuthService
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
}
