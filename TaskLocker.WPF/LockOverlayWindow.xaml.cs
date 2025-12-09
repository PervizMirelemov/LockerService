using System.ComponentModel; // Добавлено для CancelEventArgs
using System.Windows;

namespace TaskLocker.WPF.Views
{
    public partial class LockOverlayWindow : Window
    {
        // Флаг: можно ли закрывать окно? По умолчанию - НЕТ.
        public bool AllowClose { get; set; } = false;

        public LockOverlayWindow()
        {
            InitializeComponent();
            this.Loaded += LockOverlayWindow_Loaded;
            this.Deactivated += LockOverlayWindow_Deactivated;

            // Подписываемся на событие попытки закрытия окна
            this.Closing += LockOverlayWindow_Closing;
        }

        private void LockOverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Activate();
            this.Focus();
        }

        private void LockOverlayWindow_Deactivated(object? sender, System.EventArgs e)
        {
            this.Topmost = true;
            this.Activate();
        }

        // ГЛАВНОЕ ИЗМЕНЕНИЕ: Запрет закрытия
        private void LockOverlayWindow_Closing(object? sender, CancelEventArgs e)
        {
            // Если мы (программа) не разрешили закрытие -> отменяем закрытие
            if (!AllowClose)
            {
                e.Cancel = true;
            }
        }

        public void UpdateTimer(int secondsLeft)
        {
            CountdownText.Text = $"Автоматическая разблокировка через {secondsLeft} сек...";
        }
    }
}