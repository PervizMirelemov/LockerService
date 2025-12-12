using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
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
        private readonly IWindowManagementService _windowService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<Worker> _logger;

        // НОВОЕ ИМЯ: Чтобы сбросить любые старые настройки "Disabled"
        private const string RegistryName = "TaskLocker_Auto";

        private bool _isSystemService = false;

        public Worker(ILogger<Worker> logger, IWindowManagementService windowService, IConfiguration configuration)
        {
            _logger = logger;
            _windowService = windowService;
            _configuration = configuration;
            LogToFile("Worker initialized (64-bit mode).");
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            string user = Environment.UserName;
            _isSystemService = user.Contains("SYSTEM", StringComparison.OrdinalIgnoreCase) || user.Contains("$");

            LogToFile($"StartAsync called. User: {user}, IsSystem: {_isSystemService}");

            if (_isSystemService)
            {
                // ВКЛЮЧАЕМ автозагрузку
                SetGlobalStartup(true);
            }

            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            LogToFile("StopAsync called.");

            if (_isSystemService)
            {
                // ОТКЛЮЧАЕМ автозагрузку
                SetGlobalStartup(false);
            }

            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(3000, stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    if (!_isSystemService && IsUserAllowed())
                    {
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            if (!_windowService.IsShutdownDialogVisible())
                                _windowService.ShowShutdownDialog();
                        });

                        await Task.Delay(_windowService.NextShowDelay, stoppingToken);
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error in ExecuteAsync: {ex.Message}");
            }
        }

        // --- ЛОГИКА АВТОЗАГРУЗКИ (64-BIT FIX) ---
        private void SetGlobalStartup(bool enable)
        {
            try
            {
                LogToFile($"SetGlobalStartup: {enable}");

                string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
                string approvedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

                // ГЛАВНОЕ ИЗМЕНЕНИЕ: Используем RegistryView.Registry64
                // Это заставляет писать в реальный HKLM, а не в Wow6432Node
                using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

                // 1. Управление ключом RUN
                using (var key = localMachine.OpenSubKey(runKeyPath, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string exePath = Environment.ProcessPath ?? "";
                            if (!string.IsNullOrEmpty(exePath))
                            {
                                key.SetValue(RegistryName, exePath);
                                LogToFile($"Registry (64-bit) SET {RegistryName} -> {exePath}");
                            }
                        }
                        else
                        {
                            if (key.GetValue(RegistryName) != null)
                            {
                                key.DeleteValue(RegistryName, false);
                                LogToFile($"Registry (64-bit) DELETED {RegistryName}");
                            }
                        }
                    }
                    else
                    {
                        LogToFile("Error: Could not open HKLM Run key (64-bit view).");
                    }
                }

                // 2. СБРОС StartupApproved (Тоже в 64-bit)
                if (enable)
                {
                    try
                    {
                        using (var approvedKey = localMachine.OpenSubKey(approvedKeyPath, true))
                        {
                            if (approvedKey != null && approvedKey.GetValue(RegistryName) != null)
                            {
                                approvedKey.DeleteValue(RegistryName, false);
                                LogToFile("Cleared 'StartupApproved' status in 64-bit registry.");
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"CRITICAL REGISTRY ERROR: {ex}");
            }
        }

        private bool IsUserAllowed()
        {
            try
            {
                string? configPath = _configuration.GetValue<string>("UserListPath");
                string exeFolder = AppDomain.CurrentDomain.BaseDirectory;
                string defaultPath = Path.Combine(exeFolder, "users.txt");
                string finalPath = !string.IsNullOrWhiteSpace(configPath) ? configPath : defaultPath;

                if (!File.Exists(finalPath)) return false;

                var allowedUsers = File.ReadAllLines(finalPath)
                                       .Select(line => line.Trim())
                                       .Where(line => !string.IsNullOrEmpty(line));

                return allowedUsers.Contains(Environment.UserName, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void LogToFile(string message)
        {
            try
            {
                string logFile = @"C:\Users\Public\TaskLocker_Worker_Log.txt";
                File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}