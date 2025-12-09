using System;
using System.Windows;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace TaskLocker.WPF
{
    // Обычный класс, наследующий Application
    public class App : System.Windows.Application
    {
        private Forms.NotifyIcon? _notifyIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Настройка: приложение не закрывается, если закрыты окна
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            CreateTrayIcon();
        }

        private void CreateTrayIcon()
        {
            _notifyIcon = new Forms.NotifyIcon();
            _notifyIcon.Icon = Drawing.SystemIcons.Shield;
            _notifyIcon.Text = "Task Locker Service";
            _notifyIcon.Visible = true;

            var contextMenu = new Forms.ContextMenuStrip();
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

        protected override void OnExit(ExitEventArgs e)
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