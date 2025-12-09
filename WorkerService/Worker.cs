using Microsoft.Extensions.Configuration; // Для IConfiguration
using Microsoft.Extensions.Hosting;       // Для BackgroundService
using Microsoft.Extensions.Logging;       // Для ILogger
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskLocker.WPF.Services;

namespace WorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IWindowManagementService _windowService;
        private readonly IConfiguration _configuration;

        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(20);
        private readonly TimeSpan _retryInterval = TimeSpan.FromSeconds(30);

        public Worker(ILogger<Worker> logger, IWindowManagementService windowService, IConfiguration configuration)
        {
            _logger = logger;
            _windowService = windowService;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker Service started.");

            // Автозапуск при старте
            RegisterInStartup();

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (IsUserAllowed())
                    {
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            if (!_windowService.IsShutdownDialogVisible())
                            {
                                _windowService.ShowShutdownDialog();
                            }
                        });

                        TimeSpan delay = _windowService.NextShowDelay > TimeSpan.Zero
                            ? _windowService.NextShowDelay
                            : _checkInterval;

                        if (_windowService.NextShowDelay > TimeSpan.Zero)
                            _windowService.NextShowDelay = TimeSpan.Zero;

                        _logger.LogInformation("Next check in {minutes} minutes.", delay.TotalMinutes);
                        await Task.Delay(delay, stoppingToken);
                    }
                    else
                    {
                        _logger.LogDebug("User not allowed. Retrying shortly.");
                        await Task.Delay(_retryInterval, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Worker loop");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private bool IsUserAllowed()
        {
            try
            {
                string? configPath = _configuration.GetValue<string>("UserListPath");
                string exePath = AppDomain.CurrentDomain.BaseDirectory;
                string defaultPath = Path.Combine(exePath, "users.txt");
                string finalPath = configPath ?? defaultPath;

                if (!File.Exists(finalPath)) return false;

                var allowedUsers = File.ReadAllLines(finalPath)
                                       .Select(line => line.Trim())
                                       .Where(line => !string.IsNullOrEmpty(line));

                string currentUser = Environment.UserName;
                return allowedUsers.Contains(currentUser, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void RegisterInStartup()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;

                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    key.SetValue("TaskLockerService", exePath);
                }
            }
            catch { }
        }
    }
}