using System.Windows.Input;

namespace SimpleApp.ViewModels;

public sealed class SettingsViewModel
{
    public bool DarkMode { get; set; }
    public ICommand SaveCommand { get; }

    public SettingsViewModel()
    {
        SaveCommand = new RelayCommand(() => { });
    }
}
