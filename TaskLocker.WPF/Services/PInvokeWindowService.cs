using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows; // Для Window, WindowState и т.д.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskLocker.WPF.Native;
using TaskLocker.WPF.ViewModels;
using TaskLocker.WPF.Views;

// Псевдоним для WinForms
using WinForms = System.Windows.Forms;

namespace TaskLocker.WPF.Services
{
    public class PInvokeWindowService : IWindowManagementService
    {
        private const string DialogClassName = "#32770";
        private const string WindowTitle_RU = "Завершение работы Windows";
        private const string WindowTitle_EN = "Shut Down Windows";

        private readonly ILogger<PInvokeWindowService> _logger;
        private readonly IServiceProvider _serviceProvider;

        private readonly List<Window> _dialogWindows = new();
        private readonly List<Window> _lockWindows = new();

        private readonly object _dialogLock = new();
        private System.Windows.Threading.DispatcherFrame? _dispatcherFrame;

        public TimeSpan NextShowDelay { get; set; } = TimeSpan.Zero;

        public PInvokeWindowService(ILogger<PInvokeWindowService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async void StartPseudoLock(TimeSpan duration)
        {
            _logger.LogInformation("Starting pseudo-lock for {Duration} seconds.", duration.TotalSeconds);

            // ИСПРАВЛЕНО: Явное указание System.Windows.Application
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            await dispatcher.InvokeAsync(async () =>
            {
                _lockWindows.Clear();

                foreach (var screen in WinForms.Screen.AllScreens)
                {
                    var lockWin = new LockOverlayWindow();
                    lockWin.WindowStartupLocation = WindowStartupLocation.Manual;
                    lockWin.WindowState = WindowState.Normal;
                    lockWin.Left = screen.Bounds.Left;
                    lockWin.Top = screen.Bounds.Top;
                    lockWin.Show();
                    lockWin.WindowState = WindowState.Maximized;
                    _lockWindows.Add(lockWin);
                }

                int secondsLeft = (int)duration.TotalSeconds;

                while (secondsLeft > 0)
                {
                    foreach (var win in _lockWindows)
                    {
                        if (win is LockOverlayWindow lockScreen)
                        {
                            lockScreen.UpdateTimer(secondsLeft);
                            lockScreen.Topmost = true;
                            lockScreen.Activate();
                        }
                    }
                    await Task.Delay(1000);
                    secondsLeft--;
                }

                foreach (var win in _lockWindows)
                {
                    if (win is LockOverlayWindow lockScreen)
                    {
                        lockScreen.AllowClose = true;
                        lockScreen.Close();
                    }
                }
                _lockWindows.Clear();
                _logger.LogInformation("Pseudo-lock finished.");
            });
        }

        public void ShowShutdownDialog()
        {
            // ИСПРАВЛЕНО: Явное указание System.Windows.Application
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
                dispatcher.Invoke(ShowFallbackDialogInternal);
        }

        private void ShowFallbackDialogInternal()
        {
            lock (_dialogLock)
            {
                if (_dialogWindows.Any(w => w.IsVisible)) return;

                _dialogWindows.Clear();
                var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                var screens = WinForms.Screen.AllScreens;

                foreach (var screen in screens)
                {
                    var window = new MainWindow
                    {
                        DataContext = viewModel,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        WindowState = WindowState.Normal
                    };

                    window.Left = screen.Bounds.Left;
                    window.Top = screen.Bounds.Top;
                    window.Show();
                    window.WindowState = WindowState.Maximized;
                    _dialogWindows.Add(window);
                }

                if (_dialogWindows.Count > 0)
                {
                    _dispatcherFrame = new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.PushFrame(_dispatcherFrame);
                }

                _dispatcherFrame = null;
                _dialogWindows.Clear();
            }
        }

        public void HideShutdownDialog()
        {
            // ИСПРАВЛЕНО: Явное указание System.Windows.Application
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            Action closeAction = () =>
            {
                lock (_dialogLock)
                {
                    foreach (var window in _dialogWindows)
                    {
                        if (window is MainWindow mw) mw.AllowClose = true;
                        window.Close();
                    }
                    _dialogWindows.Clear();
                    if (_dispatcherFrame != null) _dispatcherFrame.Continue = false;
                }
            };

            if (dispatcher != null) dispatcher.Invoke(closeAction);
        }

        public bool IsShutdownDialogVisible()
        {
            // ИСПРАВЛЕНО: Явное указание System.Windows.Application
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                return dispatcher.Invoke(() =>
                    _dialogWindows.Any(w => w.IsVisible) || _lockWindows.Any(w => w.IsVisible));
            }
            return false;
        }

        // Заглушка
        public bool LockWorkStation() { return true; }
    }
}