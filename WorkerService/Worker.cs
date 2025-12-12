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

        private const string CurrentRegistryName = "TaskLocker_Auto";

        // Старые имена для очистки
        private readonly string[] _legacyNames = new[]
        {
            "TaskLockerService", "TaskLocker_Global", "WorkerService", "TaskLocker"
        };

        private bool _isSystemService = false;

        public Worker(ILogger<Worker> logger, IWindowManagementService windowService, IConfiguration configuration)
        {
            _logger = logger;
            _windowService = windowService;
            _configuration = configuration;
            LogToFile(">>> SERVICE INITIALIZED <<<");
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            string user = Environment.UserName;
            // Определяем, работаем ли мы от имени Системы (Служба)
            _isSystemService = user.Contains("SYSTEM", StringComparison.OrdinalIgnoreCase) || user.Contains("$");

            LogToFile($"[START] StartAsync called. User: {user}, IsSystem: {_isSystemService}");

            if (_isSystemService)
            {
                // При старте: удаляем мусор и ставим правильный ключ
                CleanupAllRegistryKeys();
                SetGlobalStartup(true);
            }

            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            LogToFile("[STOP] StopAsync called! Initiating cleanup...");

            if (_isSystemService)
            {
                try
                {
                    // 1. Пытаемся удалить ключи
                    CleanupAllRegistryKeys();

                    // 2. ПРОВЕРКА: Действительно ли удалилось?
                    bool exists = CheckIfKeyExists();
                    if (exists)
                    {
                        LogToFile("[STOP FAILED] WARNING! The key still exists after cleanup!");
                    }
                    else
                    {
                        LogToFile("[STOP SUCCESS] Verification passed: Key is gone from Registry.");
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"[STOP ERROR] Exception during StopAsync: {ex}");
                }
            }
            else
            {
                LogToFile("[STOP] Not a System Service, skipping registry cleanup.");
            }

            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(3000, stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Логика блокировки для пользователя
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
            catch (OperationCanceledException)
            {
                LogToFile("[EXECUTE] Task cancelled (Service stopping).");
            }
            catch (Exception ex)
            {
                LogToFile($"[EXECUTE ERROR] {ex.Message}");
            }
        }

        // --- РАБОТА С РЕЕСТРОМ ---

        private void CleanupAllRegistryKeys()
        {
            var views = new[] { RegistryView.Registry64, RegistryView.Registry32 };

            foreach (var view in views)
            {
                try
                {
                    using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using var key = localMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);

                    if (key != null)
                    {
                        // Удаляем старые имена
                        foreach (var oldName in _legacyNames)
                        {
                            if (key.GetValue(oldName) != null)
                            {
                                key.DeleteValue(oldName, false);
                                LogToFile($"[CLEANUP] Deleted legacy '{oldName}' from {view}");
                            }
                        }

                        // Удаляем текущее имя (для StopAsync или пересоздания)
                        if (key.GetValue(CurrentRegistryName) != null)
                        {
                            key.DeleteValue(CurrentRegistryName, false);
                            LogToFile($"[CLEANUP] Deleted CURRENT '{CurrentRegistryName}' from {view}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"[CLEANUP ERROR] View {view}: {ex.Message}");
                }
            }
        }

        private void SetGlobalStartup(bool enable)
        {
            // Если нужно включить - пишем ключ. (Если выключить - мы его уже удалили в Cleanup)
            if (enable)
            {
                try
                {
                    using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                    using var key = localMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);

                    if (key != null)
                    {
                        string exePath = Environment.ProcessPath ?? "";
                        key.SetValue(CurrentRegistryName, exePath);
                        LogToFile($"[SET] Created key '{CurrentRegistryName}' -> '{exePath}'");
                    }

                    // Сброс StartupApproved (чтобы было Enabled)
                    using var approvedKey = localMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", true);
                    if (approvedKey != null && approvedKey.GetValue(CurrentRegistryName) != null)
                    {
                        approvedKey.DeleteValue(CurrentRegistryName, false);
                        LogToFile("[SET] Cleared StartupApproved (Forced Enable).");
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"[SET ERROR] {ex.Message}");
                }
            }
        }

        private bool CheckIfKeyExists()
        {
            try
            {
                using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = localMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue(CurrentRegistryName) != null;
            }
            catch
            {
                return false;
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