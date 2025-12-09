using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskLocker.WPF.Services;

namespace TaskLocker.WPF.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IWindowManagementService _windowService;

        public string CurrentUserName { get; }

        public MainViewModel(IWindowManagementService windowService)
        {
            _windowService = windowService;
            CurrentUserName = Environment.UserName;
        }

        [RelayCommand]
        private void Ok()
        {
            // 1. Устанавливаем, что следующее окно появится через 20 минут
            _windowService.NextShowDelay = TimeSpan.FromMinutes(3);

            // 2. Скрываем текущее окно с кнопкой "ОК"
            _windowService.HideShutdownDialog();

            // 3. Запускаем "псевдо-блокировку" на 30 секунд
            // (Черный экран, который сам исчезнет)
            _windowService.StartPseudoLock(TimeSpan.FromSeconds(30));
        }
    }
}