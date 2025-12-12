using System;
using Microsoft.Win32; // Для работы с реестром
// ВАЖНО: Разделяем пространства имен, чтобы не было конфликтов
using Wpf = System.Windows;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace TaskLocker.WPF
{
    // Наследуемся от WPF Application
    public class App : Wpf.Application
    {
        private Forms.NotifyIcon? _notifyIcon;

        // Старое имя, которое мы хотим удалить из личной автозагрузки, чтобы не было дублей
        private const string OldAppName = "TaskLockerService";

        protected override void OnStartup(Wpf.StartupEventArgs e)
        {
            base.OnStartup(e);

            // Чтобы приложение не закрывалось, когда закрыто главное окно
            this.ShutdownMode = Wpf.ShutdownMode.OnExplicitShutdown;

            // 1. ОЧИСТКА: Удаляем запись из ЛИЧНОЙ автозагрузки (HKCU),
            // так как мы теперь используем ГЛОБАЛЬНУЮ (HKLM) через службу.
            RemoveUserStartup();

            CreateTrayIcon();
        }

        private void RemoveUserStartup()
        {
            try
            {
                // Открываем ветку автозагрузки ТЕКУЩЕГО ПОЛЬЗОВАТЕЛЯ
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    // Если нашли старую запись — удаляем её, чтобы не было двух строк в диспетчере
                    if (key.GetValue(OldAppName) != null)
                    {
                        key.DeleteValue(OldAppName, false);
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки (например, если нет прав или ключа)
            }
        }

        private void CreateTrayIcon()
        {
            _notifyIcon = new Forms.NotifyIcon();
            // Используем стандартную иконку щита, можно заменить на свою
            _notifyIcon.Icon = Drawing.SystemIcons.Shield;
            _notifyIcon.Text = "Task Locker Service";
            _notifyIcon.Visible = true;

            var contextMenu = new Forms.ContextMenuStrip();

            // Кнопка выхода
            var exitItem = new Forms.ToolStripMenuItem("Выход");
            exitItem.Click += (s, e) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                Environment.Exit(0);
            };

            contextMenu.Items.Add(exitItem);
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        protected override void OnExit(Wpf.ExitEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.OnExit(e);
        }
    }
}