using System.Windows;
using NetworkTrafficGuard.Tray.ViewModels;

namespace NetworkTrafficGuard.Tray;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
