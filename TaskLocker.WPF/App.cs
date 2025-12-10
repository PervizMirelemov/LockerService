using System;
using System.Diagnostics;
using Microsoft.Win32;

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
        private const string AppName = "TaskLockerService";

        protected override void OnStartup(Wpf.StartupEventArgs e)
        {
            base.OnStartup(e);

            // Теперь это работает, так как мы наследники Wpf.Application
            this.ShutdownMode = Wpf.ShutdownMode.OnExplicitShutdown;

            RegisterInStartup();
            CreateTrayIcon();
        }

        private void CreateTrayIcon()
        {
            _notifyIcon = new Forms.NotifyIcon();
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

        private void RegisterInStartup()
        {
            try
            {
                // Получаем путь к EXE (это будет WorkerService.exe)
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    if (key != null)
                    {
                        key.SetValue(AppName, exePath);
                    }
                }
            }
            catch { }
        }
    }
}