using System;

namespace TaskLocker.WPF.Services
{
    public interface IWindowManagementService
    {
        void ShowShutdownDialog();
        void HideShutdownDialog();
        bool IsShutdownDialogVisible();

        // Удаляем LockWorkStation, добавляем StartPseudoLock
        void StartPseudoLock(TimeSpan duration);

        TimeSpan NextShowDelay { get; set; }
    }
}