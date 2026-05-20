using System.Windows.Input;

namespace SimpleApp.ViewModels;

public sealed class RegisterViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public ICommand RegisterCommand { get; }

    public RegisterViewModel(Services.IAuthService authService)
    {
        RegisterCommand = new RelayCommand(async () => await authService.RegisterAsync(Username, Email, string.Empty));
    }
}
