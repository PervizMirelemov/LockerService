using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TaskLocker.WPF;
using TaskLocker.WPF.Services;
using TaskLocker.WPF.ViewModels;
using WorkerService;

// Простой логгер (оставил, чтобы вы могли проверить работу, если что-то пойдет не так)
public static class SimpleLog
{
    private static readonly string LogPath = @"C:\Users\Public\TaskLocker_Debug.txt";
    private static readonly object _lock = new object();

    public static void Write(string message)
    {
        try
        {
            lock (_lock)
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}

public class Program
{
    // НОВОЕ имя для реестра. Изменение имени сбросит статус "Disabled" на "Enabled"!
    private const string GlobalRegistryName = "TaskLocker_Global";

    // Старое имя для очистки
    private const string OldRegistryName = "TaskLockerService";

    [STAThread]
    public static void Main(string[] args)
    {
        // Логирование запуска для диагностики
        SimpleLog.Write($"Program Main Started. Args: {string.Join(" ", args)}");

        // 1. ЛОГИКА ДЛЯ УСТАНОВЩИКА (Создание Службы и записи в реестр)
        if (args.Contains("/Install"))
        {
            InstallWindowsService();
            return; // Выходим после установки
        }

        if (args.Contains("/Uninstall"))
        {
            UninstallWindowsService();
            return; // Выходим после удаления
        }

        // 2. ЗАПУСК ПРИЛОЖЕНИЯ
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

        // Запускаем хост (Службу или консоль)
        host.Start();

        // Запускаем UI (WPF)
        // Важно: UI будет виден, только если запущено пользователем, а не службой в Session 0.
        // Но так как мы прописали путь к EXE в реестр Run, при входе пользователя
        // запустится именно этот код под пользователем.
        var app = host.Services.GetRequiredService<App>();
        app.Run();

        host.StopAsync().Wait();
    }

    // --- УПРАВЛЕНИЕ СЛУЖБОЙ И АВТОЗАГРУЗКОЙ ---
    static void InstallWindowsService()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? "";
            string binPath = $"\"{exePath}\""; // Кавычки обязательны!
            string serviceName = "TaskLockerService";

            SimpleLog.Write($"Installing Service from: {exePath}");

            // 1. Создаем службу (чтобы работала защита от убийства процесса и т.д.)
            RunSc($"create \"{serviceName}\" binPath= {binPath} start= auto DisplayName= \"Task Locker Controller\"");
            // Настраиваем перезапуск при сбое
            RunSc($"failure \"{serviceName}\" reset= 0 actions= restart/60000");
            // Запускаем службу
            RunSc($"start \"{serviceName}\"");

            // 2. ПРОПИСЫВАЕМ В АВТОЗАГРУЗКУ (HKLM - для всех пользователей)
            using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                // Сначала удаляем СТАРУЮ запись, которая могла быть "Disabled"
                if (key.GetValue(OldRegistryName) != null)
                {
                    SimpleLog.Write("Deleting old registry key...");
                    key.DeleteValue(OldRegistryName, false);
                }

                // Создаем НОВУЮ запись. Для Windows это "новая" программа -> она будет Enabled по умолчанию.
                SimpleLog.Write($"Setting new registry key: {GlobalRegistryName}");
                key.SetValue(GlobalRegistryName, exePath);
            }
        }
        catch (Exception ex)
        {
            SimpleLog.Write($"Install Error: {ex.Message}");
        }
    }

    static void UninstallWindowsService()
    {
        try
        {
            SimpleLog.Write("Uninstalling...");

            using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                // Удаляем нашу глобальную запись
                if (key.GetValue(GlobalRegistryName) != null)
                    key.DeleteValue(GlobalRegistryName, false);

                // На всякий случай чистим и старое имя
                if (key.GetValue(OldRegistryName) != null)
                    key.DeleteValue(OldRegistryName, false);
            }

            RunSc("stop \"TaskLockerService\"");
            RunSc("delete \"TaskLockerService\"");
        }
        catch (Exception ex)
        {
            SimpleLog.Write($"Uninstall Error: {ex.Message}");
        }
    }

    static void RunSc(string arguments)
    {
        try
        {
            var p = new Process();
            p.StartInfo.FileName = "sc.exe";
            p.StartInfo.Arguments = arguments;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            p.WaitForExit();
        }
        catch { }
    }
}