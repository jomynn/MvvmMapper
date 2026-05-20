using System.Windows.Input;
using SharedVmApp.Services;

namespace SharedVmApp.ViewModels;

public sealed class AuthViewModel
{
    private readonly IAuthService _authService;

    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand ForgotPasswordCommand { get; }

    public AuthViewModel(IAuthService authService)
    {
        _authService = authService;
        LoginCommand = new RelayCommand(async () => await _authService.LoginAsync(Username, string.Empty));
        RegisterCommand = new RelayCommand(async () => await _authService.RegisterAsync(Username, Email, string.Empty));
        ForgotPasswordCommand = new RelayCommand(async () => await _authService.ForgotPasswordAsync(Email));
    }

    // Parameterless constructor for XAML instantiation
    public AuthViewModel() : this(new AuthService(new System.Net.Http.HttpClient()))
    {
    }

    private sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
