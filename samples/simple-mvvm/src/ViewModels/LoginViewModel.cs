using System.Windows.Input;

namespace SimpleApp.ViewModels;

public sealed class LoginViewModel
{
    public string Username { get; set; } = string.Empty;
    public ICommand LoginCommand { get; }

    public LoginViewModel(Services.IAuthService authService)
    {
        LoginCommand = new RelayCommand(async () => await authService.LoginAsync(Username, string.Empty));
    }
}
