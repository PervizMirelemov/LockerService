using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32; // Реестр
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

        private const string RegistryAppName = "TaskLockerAgent";

        // Флаг: мы работаем как Служба (SYSTEM) или как Агент (User)?
        private bool _isSystemService = false;

        public Worker(ILogger<Worker> logger, IWindowManagementService windowService, IConfiguration configuration)
        {
            _logger = logger;
            _windowService = windowService;
            _configuration = configuration;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            // Проверяем, кто мы: SYSTEM или Пользователь
            string user = Environment.UserName;
            _isSystemService = user.Contains("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
                               user.Contains("$"); // Аккаунты служб часто заканчиваются на $

            if (_isSystemService)
            {
                // ЛОГИКА СЛУЖБЫ: Включить автозагрузку для всех
                SetGlobalStartup(true);
            }

            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            if (_isSystemService)
            {
                // ЛОГИКА СЛУЖБЫ: Службу останавливают -> Отключаем автозагрузку
                SetGlobalStartup(false);
            }

            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Если мы SYSTEM, нам не нужно показывать окна, мы только управляем автозапуском.
            // Но чтобы не усложнять, пусть код идет дальше, в Session 0 окна просто не покажутся.

            // Если мы Пользователь (Агент), мы показываем окна.

            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                // ... ВАША ЛОГИКА БЛОКИРОВКИ ...
                if (!_isSystemService && IsUserAllowed()) // Проверяем юзера только если мы не служба
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        if (!_windowService.IsShutdownDialogVisible())
                            _windowService.ShowShutdownDialog();
                    });

                    // ... ожидание ...
                    await Task.Delay(_windowService.NextShowDelay, stoppingToken);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }

        // --- УПРАВЛЕНИЕ РЕЕСТРОМ (ДЛЯ ВСЕХ ПОЛЬЗОВАТЕЛЕЙ) ---
        private void SetGlobalStartup(bool enable)
        {
            try
            {
                // HKLM требует прав SYSTEM или Admin (у службы они есть)
                using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                if (enable)
                {
                    string exePath = Environment.ProcessPath;
                    key.SetValue(RegistryAppName, exePath);
                }
                else
                {
                    // Удаляем запись
                    if (key.GetValue(RegistryAppName) != null)
                        key.DeleteValue(RegistryAppName, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Registry Error: {ex.Message}");
            }
        }

        private bool IsUserAllowed()
        {
            try
            {
                // 1. Берем путь из конфига или ищем рядом с exe
                string? configPath = _configuration.GetValue<string>("UserListPath");

                string exeFolder = AppDomain.CurrentDomain.BaseDirectory;
                string defaultPath = Path.Combine(exeFolder, "users.txt");

                // Если в конфиге пусто, берем дефолтный
                string finalPath = !string.IsNullOrWhiteSpace(configPath) ? configPath : defaultPath;

                if (!File.Exists(finalPath))
                {
                    LogToFile($"Файл не найден: {finalPath}");
                    return false;
                }

                var allowedUsers = File.ReadAllLines(finalPath)
                                       .Select(line => line.Trim())
                                       .Where(line => !string.IsNullOrEmpty(line));

                string currentUser = Environment.UserName;

                // Для теста, если служба работает под SYSTEM, можно вернуть true
                // return true; 

                return allowedUsers.Contains(currentUser, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                LogToFile($"Ошибка проверки пользователя: {ex.Message}");
                return false;
            }
        }
        private void LogToFile(string message)
        {
            try
            {
                // Пишем лог на диск C, чтобы вы видели, что программа жива
                File.AppendAllText(@"C:\TaskLocker_Log.txt", $"{DateTime.Now}: {message}{Environment.NewLine}");
            }
            catch
            {
                // Игнорируем ошибки записи лога
            }
        }
    }
}