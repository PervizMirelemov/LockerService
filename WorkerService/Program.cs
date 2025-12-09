using TaskLocker.WPF;
using TaskLocker.WPF.Services;
using TaskLocker.WPF.ViewModels;
using WorkerService;

public class Program
{
    // STAThread обязателен для WPF
    [STAThread]
    public static void Main(string[] args)
    {
        // 1. Создаем строителя приложения
        var builder = Host.CreateApplicationBuilder(args);

        // 2. Настраиваем службы
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "TaskLockerService";
        });

        // --- Регистрируем зависимости из WPF проекта ---
        // Сервис для управления окнами
        builder.Services.AddSingleton<IWindowManagementService, PInvokeWindowService>();
        // ViewModel для окон
        builder.Services.AddTransient<MainViewModel>();
        // Само WPF приложение
        builder.Services.AddSingleton<App>();

        // --- Регистрируем наш фоновый "Мозг" ---
        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();

        // 3. Запускаем фоновые задачи (Worker)
        host.Start();

        // 4. Запускаем UI (WPF) в главном потоке
        // Это блокирующий вызов, пока приложение работает
        var app = host.Services.GetRequiredService<App>();
        app.InitializeComponent(); // Инициализация ресурсов из App.xaml
        app.Run();

        // 5. Когда UI закрылся (если такое случится), останавливаем всё остальное
        host.StopAsync().Wait();
    }
}