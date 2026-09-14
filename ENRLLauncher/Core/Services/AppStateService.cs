using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Utilities;

namespace ENRLLauncher.Core.Services;

public class AppStateService : ObservableObject, IAppStateService
{
    private bool _isEditMode;
    private bool _isHomeViewActive = true;

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            var targetValue = value && _isHomeViewActive;
            Set(ref _isEditMode, targetValue);
        }
    }

    public bool IsHomeViewActive
    {
        get => _isHomeViewActive;
        set
        {
            if (Set(ref _isHomeViewActive, value) && !_isHomeViewActive && _isEditMode)
            {
                IsEditMode = false;
            }
        }
    }

    public event Action<LaunchTargetType>? AddSeparatorRequested;

    public void RequestAddSeparator(LaunchTargetType targetType)
    {
        if (IsEditMode && IsHomeViewActive)
        {
            AddSeparatorRequested?.Invoke(targetType);
        }
    }
}
