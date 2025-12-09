using Microsoft.Extensions.Configuration; // Добавлено для чтения конфига
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
// Явно указываем таймер
using Timer = System.Threading.Timer;

namespace TaskLocker.WPF.Services
{
    public class ShutdownDialogMonitorService : IHostedService, IDisposable
    {
        private const int DefaultIntervalSeconds = 5;

        private readonly IWindowManagementService _windowService;
        private readonly ILogger<ShutdownDialogMonitorService> _logger;
        private readonly IConfiguration _configuration; // Поле для конфига
        private Timer? _timer;
        private int _running;

        // Внедряем IConfiguration в конструктор
        public ShutdownDialogMonitorService(
            IWindowManagementService windowService,
            ILogger<ShutdownDialogMonitorService> logger,
            IConfiguration configuration)
        {
            _windowService = windowService;
            _logger = logger;
            _configuration = configuration;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting ShutdownDialogMonitorService.");
            _timer = new Timer(DoWork, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
            return Task.CompletedTask;
        }

        private void DoWork(object? state)
        {
            if (Interlocked.Exchange(ref _running, 1) == 1) return;
            try
            {
                if (IsUserAllowed())
                {
                    if (!_windowService.IsShutdownDialogVisible())
                    {
                        _windowService.ShowShutdownDialog();
                    }
                }
                else
                {
                    _logger.LogDebug("User not in allowed list. Skipping dialog show.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during shutdown dialog monitor tick.");
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);

                TimeSpan delay;
                if (_windowService.NextShowDelay > TimeSpan.Zero)
                {
                    delay = _windowService.NextShowDelay;
                    _windowService.NextShowDelay = TimeSpan.Zero;
                }
                else
                {
                    delay = TimeSpan.FromSeconds(DefaultIntervalSeconds);
                }

                try
                {
                    _timer?.Change(delay, Timeout.InfiniteTimeSpan);
                }
                catch (ObjectDisposedException) { }
            }
        }

        private bool IsUserAllowed()
        {
            try
            {
                // Читаем путь из appsettings.json
                // Если там пусто, используем значение по умолчанию (опционально)
                string userListFilePath = _configuration.GetValue<string>("UserListPath")
                                          ?? @"C:\TaskLocker\allowed_users.txt";

                if (!File.Exists(userListFilePath))
                {
                    // _logger.LogWarning($"User list file not found at {userListFilePath}");
                    return false;
                }

                var allowedUsers = File.ReadAllLines(userListFilePath)
                                       .Select(line => line.Trim())
                                       .Where(line => !string.IsNullOrEmpty(line));

                string currentUser = Environment.UserName;

                return allowedUsers.Contains(currentUser, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read allowed users file.");
                return false;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping ShutdownDialogMonitorService.");
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}