using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SimpleApp.ViewModels;

public sealed class DashboardViewModel
{
    public ObservableCollection<string> Items { get; } = [];
    public ICommand RefreshCommand { get; }

    public DashboardViewModel()
    {
        RefreshCommand = new RelayCommand(() => { });
    }
}
