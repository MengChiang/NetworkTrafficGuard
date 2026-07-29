using System.Windows;
using System.ComponentModel;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using NetworkTrafficGuard.Tray.ViewModels;

namespace NetworkTrafficGuard.Tray;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly Forms.NotifyIcon _notifyIcon;
    private Forms.ToolStripMenuItem? _showMenuItem;
    private Forms.ToolStripMenuItem? _exitMenuItem;
    private bool _isExitRequested;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;

        _notifyIcon = CreateNotifyIcon();
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        UpdateNotifyIconText();
    }

    private Forms.NotifyIcon CreateNotifyIcon()
    {
        var contextMenu = new Forms.ContextMenuStrip();
        _showMenuItem = new Forms.ToolStripMenuItem(_viewModel.Texts.ShowWindow, null, (_, _) => RestoreFromTray());
        _exitMenuItem = new Forms.ToolStripMenuItem(_viewModel.Texts.ExitApplication, null, (_, _) =>
        {
            _isExitRequested = true;
            Close();
        });
        contextMenu.Items.Add(_showMenuItem);
        contextMenu.Items.Add(_exitMenuItem);

        var notifyIcon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            ContextMenuStrip = contextMenu,
            Text = "Network Traffic Guard",
            Visible = true
        };

        notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
        return notifyIcon;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.TrayToolTipText))
        {
            UpdateNotifyIconText();
        }

        if (e.PropertyName == nameof(MainWindowViewModel.Texts))
        {
            UpdateNotifyIconMenuText();
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            ShowInTaskbar = false;
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        e.Cancel = true;
        WindowState = WindowState.Minimized;
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void UpdateNotifyIconText()
    {
        _notifyIcon.Text = TrimNotifyIconText(_viewModel.TrayToolTipText);
    }

    private void UpdateNotifyIconMenuText()
    {
        if (_showMenuItem is not null)
        {
            _showMenuItem.Text = _viewModel.Texts.ShowWindow;
        }

        if (_exitMenuItem is not null)
        {
            _exitMenuItem.Text = _viewModel.Texts.ExitApplication;
        }
    }

    private static string TrimNotifyIconText(string text)
    {
        var normalized = string.IsNullOrWhiteSpace(text)
            ? "Network Traffic Guard"
            : text.ReplaceLineEndings(" ");

        return normalized.Length <= 63
            ? normalized
            : normalized[..60] + "...";
    }
}
