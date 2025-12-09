using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows;
using TaskLocker.WPF.Native;
using TaskLocker.WPF.ViewModels;
using TaskLocker.WPF.Views;
// Явно указываем, что Application - это WPF Application
using Application = System.Windows.Application;
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

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            await dispatcher.InvokeAsync(async () =>
            {
                _lockWindows.Clear();

                // Проходим по ВСЕМ подключенным экранам
                foreach (var screen in WinForms.Screen.AllScreens)
                {
                    var lockWin = new LockOverlayWindow();

                    // 1. Обязательно разрешаем ручное позиционирование
                    lockWin.WindowStartupLocation = WindowStartupLocation.Manual;

                    // 2. Сначала ставим режим "Normal", чтобы окно можно было двигать
                    lockWin.WindowState = WindowState.Normal;

                    // 3. Ставим окно в левый верхний угол конкретного монитора
                    // Важно: используем координаты из WinForms
                    lockWin.Left = screen.Bounds.Left;
                    lockWin.Top = screen.Bounds.Top;

                    // 4. Сначала показываем окно (оно появится на нужном мониторе, но может быть маленьким)
                    lockWin.Show();

                    // 5. И ТОЛЬКО ТЕПЕРЬ разворачиваем на весь экран
                    // Это гарантирует, что оно заполнит именно ЭТОТ монитор
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

                            // Удерживаем поверх остальных окон
                            lockScreen.Topmost = true;
                            // Активируем (фокусируем) хотя бы одно окно, чтобы перехватывать ввод
                            if (win == _lockWindows.Last())
                            {
                                lockScreen.Activate();
                            }
                        }
                    }

                    await Task.Delay(1000);
                    secondsLeft--;
                }

                // ВРЕМЯ ВЫШЛО: Разрешаем закрытие и закрываем
                foreach (var win in _lockWindows)
                {
                    if (win is LockOverlayWindow lockScreen)
                    {
                        lockScreen.AllowClose = true;
                        lockScreen.Close();
                    }
                }
                _lockWindows.Clear();
                _logger.LogInformation("Pseudo-lock finished. System unlocked.");
            });
        }

        // --- ОСТАЛЬНОЙ КОД БЕЗ ИЗМЕНЕНИЙ ---

        public void ShowShutdownDialog()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
                dispatcher.Invoke(ShowFallbackDialogInternal);
        }

        // ... (остальной код класса PInvokeWindowService)

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
                        // Важно: отключаем авто-позиционирование, мы сами зададим его
                        WindowStartupLocation = WindowStartupLocation.Manual
                    };

                    // --- ИЗМЕНЕНИЯ ЗДЕСЬ ---

                    // 1. Сначала ставим обычный режим
                    window.WindowState = WindowState.Normal;

                    // 2. Перемещаем окно в верхний левый угол конкретного монитора
                    window.Left = screen.Bounds.Left;
                    window.Top = screen.Bounds.Top;

                    // 3. Показываем окно
                    window.Show();

                    // 4. Разворачиваем на весь экран. 
                    // Так как в XAML фон полупрозрачный, он закроет весь монитор затемнением.
                    window.WindowState = WindowState.Maximized;

                    _dialogWindows.Add(window);
                }

                // Запускаем цикл ожидания (DispatcherFrame), чтобы код "ждал" закрытия
                if (_dialogWindows.Count > 0)
                {
                    _dispatcherFrame = new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.PushFrame(_dispatcherFrame);
                }

                _dispatcherFrame = null;
                _dialogWindows.Clear();
            }
        }
        // ...

        public void HideShutdownDialog()
        {
            var dispatcher = Application.Current?.Dispatcher;
            Action closeAction = () =>
            {
                lock (_dialogLock)
                {
                    foreach (var window in _dialogWindows)
                    {
                        // ВАЖНО: Приводим к типу MainWindow и разрешаем закрытие
                        if (window is MainWindow mw)
                        {
                            mw.AllowClose = true;
                        }

                        // Теперь окно закроется, так как мы дали разрешение
                        window.Close();
                    }
                    _dialogWindows.Clear();
                    if (_dispatcherFrame != null) _dispatcherFrame.Continue = false;
                }
            };

            if (dispatcher != null) dispatcher.Invoke(closeAction);
            else closeAction();
        }

        public bool IsShutdownDialogVisible()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                return dispatcher.Invoke(() =>
                    _dialogWindows.Any(w => w.IsVisible) || _lockWindows.Any(w => w.IsVisible));
            }
            return false;
        }

        public bool LockWorkStation()
        {
            return true;
        }

        private IntPtr FindShutdownWindowHandle()
        {
            IntPtr hWnd = NativeMethods.FindWindowW(DialogClassName, WindowTitle_RU);
            if (hWnd != IntPtr.Zero) return hWnd;
            return NativeMethods.FindWindowW(DialogClassName, WindowTitle_EN);
        }
    }
}