using System.Windows.Input;

namespace SimpleApp.ViewModels;

public sealed class ProfileViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public ICommand EditCommand { get; }

    public ProfileViewModel()
    {
        EditCommand = new RelayCommand(() => { });
    }
}
