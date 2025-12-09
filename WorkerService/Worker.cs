using System.IO;
using TaskLocker.WPF.Services;

namespace WorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IWindowManagementService _windowService;
        private readonly IConfiguration _configuration;

        // Интервалы времени
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(20); // Основной интервал
        private readonly TimeSpan _retryInterval = TimeSpan.FromSeconds(30); // Если файл не найден

        public Worker(ILogger<Worker> logger, IWindowManagementService windowService, IConfiguration configuration)
        {
            _logger = logger;
            _windowService = windowService;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker Service started.");

            // Небольшая задержка на старте
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (IsUserAllowed())
                    {
                        // ПОЛЬЗОВАТЕЛЬ НАЙДЕН -> ПОКАЗЫВАЕМ ОКНО

                        // Важно: Вызываем ShowShutdownDialog через UI Dispatcher, 
                        // так как мы находимся в фоновом потоке.
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            if (!_windowService.IsShutdownDialogVisible())
                            {
                                _windowService.ShowShutdownDialog();
                            }
                        });

                        // Определяем, сколько ждать до следующего раза
                        TimeSpan delay = _windowService.NextShowDelay > TimeSpan.Zero
                            ? _windowService.NextShowDelay
                            : _checkInterval;

                        // Сбрасываем флаг задержки (если была нажата кнопка "ОК" на 20 мин)
                        if (_windowService.NextShowDelay > TimeSpan.Zero)
                            _windowService.NextShowDelay = TimeSpan.Zero;

                        _logger.LogInformation("Next check in {minutes} minutes.", delay.TotalMinutes);
                        await Task.Delay(delay, stoppingToken);
                    }
                    else
                    {
                        // Пользователь не найден, проверяем снова через 30 сек
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
                // Читаем путь из конфига WorkerService
                string? configPath = _configuration.GetValue<string>("UserListPath");

                // Если в конфиге пусто, ищем файл users.txt рядом с exe
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
    }
}