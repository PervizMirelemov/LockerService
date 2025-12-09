using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.IO;
using TaskLocker.WPF;
using TaskLocker.WPF.Services;
using TaskLocker.WPF.ViewModels;
using WorkerService;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // === ЛОГИКА ДЛЯ УСТАНОВЩИКА ===

        // Если установщик запустил нас с флагом /Install
        if (args.Contains("/Install"))
        {
            InstallService();
            return; // ВАЖНО: Выходим сразу, чтобы установщик не завис!
        }

        // Если удаление
        if (args.Contains("/Uninstall"))
        {
            UninstallService();
            return; // ВАЖНО: Выходим!
        }

        // === ОБЫЧНЫЙ ЗАПУСК СЛУЖБЫ ===
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "TaskLockerService";
        });

        // Регистрируем сервисы (чтобы работали окна)
        builder.Services.AddSingleton<IWindowManagementService, PInvokeWindowService>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddSingleton<App>();
        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();

        host.Start();

        // Запуск UI потока (для окон)
        var app = host.Services.GetRequiredService<App>();
        // Если у вас App наследуется от Application, инициализация не нужна, если удалили XAML
        // app.InitializeComponent(); 
        app.Run();

        host.StopAsync().Wait();
    }

    // Метод регистрации службы (тот самый sc create)
    static void InstallService()
    {
        try
        {
            string exePath = Environment.ProcessPath; // Путь, куда установилась программа
            string serviceName = "TaskLockerService";
            string displayName = "Task Locker Service";

            // 1. Создаем службу
            RunSc($"create \"{serviceName}\" binPath= \"{exePath}\" start= auto DisplayName= \"{displayName}\"");

            // 2. Настраиваем перезапуск при сбое
            RunSc($"failure \"{serviceName}\" reset= 0 actions= restart/60000");

            // 3. Запускаем службу сразу
            RunSc($"start \"{serviceName}\"");
        }
        catch { /* Логирование ошибок */ }
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