using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System;
using TaskLocker.WPF;
using TaskLocker.WPF.Services;
using TaskLocker.WPF.ViewModels;
using WorkerService;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // ==========================================
        // 1. РЕЖИМ УСТАНОВКИ (Запускается установщиком)
        // ==========================================
        if (args.Contains("/Install"))
        {
            InstallService();
            return; // <--- ВАЖНО: Выходим сразу! Установка завершится успешно.
        }

        if (args.Contains("/Uninstall"))
        {
            UninstallService();
            return; // <--- ВАЖНО: Выходим!
        }

        // ==========================================
        // 2. РЕЖИМ СЛУЖБЫ (Запускается Windows)
        // ==========================================
        var builder = Host.CreateApplicationBuilder(args);

        // Настройка службы
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "TaskLockerService";
        });

        // Регистрация WPF (чтобы окна работали)
        builder.Services.AddSingleton<IWindowManagementService, PInvokeWindowService>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddSingleton<App>();

        // Регистрация логики (Воркер)
        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();

        host.Start(); // Запуск фона

        // Запуск UI
        var app = host.Services.GetRequiredService<App>();
        // app.InitializeComponent(); // Если удалил App.xaml, закомментируй это
        app.Run();

        host.StopAsync().Wait();
    }

    // --- ФУНКЦИИ ДЛЯ CMD (sc.exe) ---

    static void InstallService()
    {
        try
        {
            string exePath = Environment.ProcessPath;
            // Кавычки нужны, если путь содержит пробелы (Program Files)
            string binPath = $"\"{exePath}\"";
            string serviceName = "TaskLockerService";
            string displayName = "Task Locker Service";

            // 1. Удаляем старую (на всякий случай)
            RunSc($"stop \"{serviceName}\"");
            RunSc($"delete \"{serviceName}\"");

            // 2. Создаем новую
            RunSc($"create \"{serviceName}\" binPath= {binPath} start= auto DisplayName= \"{displayName}\"");

            // 3. Настраиваем авто-перезапуск при сбое
            RunSc($"failure \"{serviceName}\" reset= 0 actions= restart/60000");

            // 4. Запускаем
            RunSc($"start \"{serviceName}\"");
        }
        catch { }
    }

    static void UninstallService()
    {
        try
        {
            string serviceName = "TaskLockerService";
            RunSc($"stop \"{serviceName}\"");
            RunSc($"delete \"{serviceName}\"");
        }
        catch { }
    }

    static void RunSc(string arguments)
    {
        var process = new Process();
        process.StartInfo.FileName = "sc.exe";
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        process.WaitForExit();
    }
}