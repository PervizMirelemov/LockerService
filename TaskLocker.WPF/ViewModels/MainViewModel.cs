using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration; // Добавь этот using!
using TaskLocker.WPF.Services;

namespace TaskLocker.WPF.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IWindowManagementService _windowService;
        private readonly IConfiguration _configuration; // Добавили конфиг

        public string CurrentUserName { get; }

        // Добавили IConfiguration в конструктор
        public MainViewModel(IWindowManagementService windowService, IConfiguration configuration)
        {
            _windowService = windowService;
            _configuration = configuration;
            CurrentUserName = Environment.UserName;
        }

        [RelayCommand]
        private void Ok()
        {
            // 1. Читаем то же самое время из настроек
            int minutes = _configuration.GetValue<int>("IntervalMinutes", 20);

            // 2. Говорим сервису: "В следующий раз покажись через N минут"
            _windowService.NextShowDelay = TimeSpan.FromMinutes(minutes);

            // 3. Закрываем окно (и запускаем блокировку экрана)
            _windowService.HideShutdownDialog();
            _windowService.StartPseudoLock(TimeSpan.FromSeconds(30));
        }
    }
}