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
        // 1. ЛОГИКА ДЛЯ УСТАНОВЩИКА (Создание Службы)
        if (args.Contains("/Install"))
        {
            InstallWindowsService();
            return; // Выходим, установщик доволен
        }

        if (args.Contains("/Uninstall"))
        {
            UninstallWindowsService();
            return;
        }

        // 2. ЗАПУСК ПРИЛОЖЕНИЯ (И Службы, и Агента)
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "TaskLockerService";
        });

        // WPF Сервисы
        builder.Services.AddSingleton<IWindowManagementService, PInvokeWindowService>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddSingleton<App>();

        // Логика (Воркер)
        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();
        host.Start();

        // Запускаем UI только если это НЕ системная служба (чтобы не грузить Session 0)
        // Но для упрощения можно оставить запуск всегда, в Session 0 просто не будет окон.
        var app = host.Services.GetRequiredService<App>();
        // app.InitializeComponent(); // Если нужно
        app.Run();

        host.StopAsync().Wait();
    }

    // --- УПРАВЛЕНИЕ СЛУЖБОЙ (SC.EXE) ---
    static void InstallWindowsService()
    {
        try
        {
            string exePath = Environment.ProcessPath;
            string binPath = $"\"{exePath}\""; // Кавычки обязательны!
            string serviceName = "TaskLockerService";

            // Создаем службу
            RunSc($"create \"{serviceName}\" binPath= {binPath} start= auto DisplayName= \"Task Locker Controller\"");
            // Настраиваем перезапуск
            RunSc($"failure \"{serviceName}\" reset= 0 actions= restart/60000");
            // Запускаем
            RunSc($"start \"{serviceName}\"");
        }
        catch { }
    }

    static void UninstallWindowsService()
    {
        try
        {
            RunSc("stop \"TaskLockerService\"");
            RunSc("delete \"TaskLockerService\"");
        }
        catch { }
    }

    static void RunSc(string arguments)
    {
        var p = new Process();
        p.StartInfo.FileName = "sc.exe";
        p.StartInfo.Arguments = arguments;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.CreateNoWindow = true;
        p.Start();
        p.WaitForExit();
    }
}