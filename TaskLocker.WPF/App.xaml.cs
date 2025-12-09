using System;
using Application = System.Windows.Application;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using System.Windows;

namespace TaskLocker.WPF
{
    public partial class App : Application
    {
        private Forms.NotifyIcon? _notifyIcon;

        // Метод OnStartup вызывается из Program.cs (app.Run())
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Приложение не закрывается, даже если нет окон
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
            var exitItem = new Forms.ToolStripMenuItem("Выход (Тест)");
            exitItem.Click += (s, e) => Shutdown();
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