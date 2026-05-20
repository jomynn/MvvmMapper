namespace SimpleApp.Services;

public interface IAuthService
{
    Task LoginAsync(string username, string password);
    Task RegisterAsync(string username, string email, string password);
}
