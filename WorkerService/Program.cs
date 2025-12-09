using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using TaskLocker.WPF;
using TaskLocker.WPF.Services; // Вам нужно будет перенести интерфейс сюда или добавить ссылку
using TaskLocker.WPF.ViewModels;
using WorkerService;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // --- ТРЮК ДЛЯ УСТАНОВЩИКА ---
        // Если запущен с флагом /Install, прописываем автозапуск и СРАЗУ ВЫХОДИМ.
        // Установщик увидит, что процесс завершился, и скажет "Готово".
        if (args.Contains("/Install"))
        {
            RegisterInStartup();
            return;
        }

        // Если запущен с флагом /Uninstall, удаляем из автозагрузки
        if (args.Contains("/Uninstall"))
        {
            UnregisterInStartup();
            return;
        }

        // --- ОБЫЧНЫЙ ЗАПУСК ---
        // Прописываемся в автозагрузку при каждом старте (на всякий случай)
        RegisterInStartup();

        var builder = Host.CreateApplicationBuilder(args);

        // Регистрируем сервисы
        builder.Services.AddSingleton<PInvokeWindowService>();
        builder.Services.AddSingleton<IWindowManagementService>(s => s.GetRequiredService<PInvokeWindowService>());
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddSingleton<App>();

        // Наш воркер
        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();
        host.Start(); // Запуск фона

        // Запуск UI
        var app = host.Services.GetRequiredService<App>();
        app.Run();

        host.StopAsync().Wait();
    }

    static void RegisterInStartup()
    {
        try
        {
            string exePath = Environment.ProcessPath!;
            // Пробуем HKLM (для всех), если нет прав - HKCU
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            key?.SetValue("TaskLockerService", exePath);
        }
        catch { }
    }

    static void UnregisterInStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            key?.DeleteValue("TaskLockerService", false);
        }
        catch { }
    }
}