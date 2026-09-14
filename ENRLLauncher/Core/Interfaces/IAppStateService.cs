using System.ComponentModel;
using ENRLLauncher.Core.Enums;

namespace ENRLLauncher.Core.Interfaces;

public interface IAppStateService : INotifyPropertyChanged
{
    bool IsEditMode { get; set; }
    bool IsHomeViewActive { get; set; }

    event Action<LaunchTargetType>? AddSeparatorRequested;

    void RequestAddSeparator(LaunchTargetType targetType);
}
