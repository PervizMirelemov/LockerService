using System.ComponentModel; // Добавлено для CancelEventArgs
using System.Windows;

namespace TaskLocker.WPF
{
    public partial class MainWindow : Window
    {
        // По умолчанию закрывать запрещено
        public bool AllowClose { get; set; } = false;

        public MainWindow()
        {
            InitializeComponent();

            // Подписываемся на событие закрытия
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // Если флаг AllowClose не установлен в true — отменяем закрытие
            if (!AllowClose)
            {
                e.Cancel = true;
            }
        }
    }
}