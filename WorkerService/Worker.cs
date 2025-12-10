using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32; // Для реестра
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

        // Интервалы
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
            LogToFile("Служба запущена.");

            // 1. Прописываемся в автозагрузку при старте
            RegisterInStartup();

            // Даем системе прогрузиться
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (IsUserAllowed())
                    {
                        LogToFile("Пользователь в списке. Показываем окно.");

                        // Вызов окна в UI потоке
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            try
                            {
                                if (!_windowService.IsShutdownDialogVisible())
                                {
                                    _windowService.ShowShutdownDialog();
                                }
                            }
                            catch (Exception uiEx)
                            {
                                LogToFile($"Ошибка UI: {uiEx.Message}");
                            }
                        });

                        // Ждем до следующей проверки
                        TimeSpan delay = _windowService.NextShowDelay > TimeSpan.Zero
                            ? _windowService.NextShowDelay
                            : _checkInterval;

                        if (_windowService.NextShowDelay > TimeSpan.Zero)
                            _windowService.NextShowDelay = TimeSpan.Zero;

                        await Task.Delay(delay, stoppingToken);
                    }
                    else
                    {
                        // Пользователь не найден
                        await Task.Delay(_retryInterval, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"Критическая ошибка: {ex.Message}");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        // --- ВОТ ЭТИ МЕТОДЫ БЫЛИ ПОТЕРЯНЫ ---

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

        private void RegisterInStartup()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;

                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    string appName = "TaskLockerService";
                    var existingValue = key.GetValue(appName) as string;
                    if (existingValue != exePath)
                    {
                        key.SetValue(appName, exePath);
                        LogToFile($"Автозапуск обновлен: {exePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Ошибка автозапуска: {ex.Message}");
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