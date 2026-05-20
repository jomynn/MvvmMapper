using System.Windows.Input;

namespace SimpleApp.ViewModels;

public sealed class LoginViewModel
{
    private readonly Services.IAuthService _authService;

    public string Username { get; set; } = string.Empty;
    public ICommand LoginCommand { get; }

    public LoginViewModel(Services.IAuthService authService)
    {
        _authService = authService;
        LoginCommand = new RelayCommand(ExecuteLogin);
    }

    private async void ExecuteLogin()
    {
        await _authService.LoginAsync(Username, string.Empty);
    }
}
