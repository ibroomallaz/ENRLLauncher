using System.Windows.Input;
using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.Core.Utilities;

namespace ENRLLauncher.MVVM.ViewModel.Dialogs;

public enum PinDialogMode
{
    Verify,
    Setup,
    Change,
    AdminVerified
}

public sealed class PinPromptDialogViewModel : ObservableObject
{
    private readonly ISecurityService _securityService;
    private readonly IAppLogger? _logger;

    private PinDialogMode _mode;
    private string _titleText = string.Empty;
    private string _headerText = string.Empty;
    private string _subText = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private bool _isBusy;
    private string _busyMessage = string.Empty;

    public PinDialogMode Mode
    {
        get => _mode;
        private set
        {
            if (Set(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsVerifyMode));
                OnPropertyChanged(nameof(IsSetupMode));
                OnPropertyChanged(nameof(IsChangeMode));
                OnPropertyChanged(nameof(IsAdminVerifiedMode));
                UpdateTextForMode();
            }
        }
    }

    public bool IsVerifyMode => Mode == PinDialogMode.Verify;
    public bool IsSetupMode => Mode == PinDialogMode.Setup;
    public bool IsChangeMode => Mode == PinDialogMode.Change;
    public bool IsAdminVerifiedMode => Mode == PinDialogMode.AdminVerified;

    public string TitleText
    {
        get => _titleText;
        private set => Set(ref _titleText, value);
    }

    public string HeaderText
    {
        get => _headerText;
        private set => Set(ref _headerText, value);
    }

    public string SubText
    {
        get => _subText;
        private set => Set(ref _subText, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (Set(ref _errorMessage, value))
            {
                HasError = !string.IsNullOrWhiteSpace(value);
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set => Set(ref _hasError, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string BusyMessage
    {
        get => _busyMessage;
        private set => Set(ref _busyMessage, value);
    }

    // Output results
    public bool Success { get; private set; }
    public bool WasPinCleared { get; private set; }
    public bool WasPinUpdated { get; private set; }
    public bool WasAdminUnlocked { get; private set; }

    // Commands
    public ICommand CloseCommand { get; }
    public ICommand AdminOverrideCommand { get; }
    public ICommand AdminUnlockOnlyCommand { get; }
    public ICommand AdminDisablePinCommand { get; }
    public ICommand AdminSetNewPinCommand { get; }

    public event Action? RequestClose;

    public PinPromptDialogViewModel(
        ISecurityService securityService,
        PinDialogMode initialMode = PinDialogMode.Verify,
        IAppLogger? logger = null)
    {
        _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        _logger = logger;

        CloseCommand = new RelayCommand(_ => CloseDialog(false));
        AdminOverrideCommand = new RelayCommand(async _ => await ExecuteAdminOverrideAsync(), _ => IsNotBusy);
        AdminUnlockOnlyCommand = new RelayCommand(_ => ExecuteAdminUnlockOnly());
        AdminDisablePinCommand = new RelayCommand(_ => ExecuteAdminDisablePin());
        AdminSetNewPinCommand = new RelayCommand(_ => Mode = PinDialogMode.Setup);

        Mode = initialMode;
    }

    private void UpdateTextForMode()
    {
        ErrorMessage = string.Empty;

        switch (Mode)
        {
            case PinDialogMode.Verify:
                TitleText = "Enter PIN";
                HeaderText = string.Empty;
                SubText = string.Empty;
                break;

            case PinDialogMode.Setup:
                TitleText = "Create PIN";
                HeaderText = string.Empty;
                SubText = "Minimum 4 digits";
                break;

            case PinDialogMode.Change:
                TitleText = "Change PIN";
                HeaderText = string.Empty;
                SubText = string.Empty;
                break;

            case PinDialogMode.AdminVerified:
                TitleText = "Admin Options";
                HeaderText = string.Empty;
                SubText = "Admin verified. Choose an action:";
                break;
        }
    }

    // Handles verification submission from the dialog view
    public bool SubmitVerify(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            ErrorMessage = "Please enter your PIN.";
            return false;
        }

        if (_securityService.VerifyPin(pin))
        {
            _logger?.Write(AppLogLevel.Info, "PinPromptDialogVM", "PIN verified successfully.");
            Success = true;
            CloseDialog(true);
            return true;
        }

        ErrorMessage = "Incorrect PIN. Please try again.";
        _logger?.Write(AppLogLevel.Warning, "PinPromptDialogVM", "Incorrect PIN attempt.");
        return false;
    }

    // Handles setup submission from the dialog view
    public bool SubmitSetup(string newPin, string confirmPin)
    {
        if (string.IsNullOrWhiteSpace(newPin))
        {
            ErrorMessage = "Please enter a PIN.";
            return false;
        }

        if (newPin.Length < 4)
        {
            ErrorMessage = "PIN must be at least 4 digits.";
            return false;
        }

        if (newPin != confirmPin)
        {
            ErrorMessage = "PINs do not match.";
            return false;
        }

        if (_securityService.SetPin(newPin))
        {
            _logger?.Write(AppLogLevel.Info, "PinPromptDialogVM", "New PIN setup successful.");
            _securityService.SetPinLockEnabled(true);
            Success = true;
            WasPinUpdated = true;
            CloseDialog(true);
            return true;
        }

        _logger?.Write(AppLogLevel.Error, "PinPromptDialogVM", "Failed to save new PIN.");
        ErrorMessage = "Failed to save PIN. Please try again.";
        return false;
    }

    // Handles change submission from the dialog view
    public bool SubmitChange(string currentPin, string newPin, string confirmPin)
    {
        if (string.IsNullOrWhiteSpace(currentPin))
        {
            ErrorMessage = "Please enter current PIN.";
            return false;
        }

        if (!_securityService.VerifyPin(currentPin))
        {
            _logger?.Write(AppLogLevel.Warning, "PinPromptDialogVM", "Incorrect current PIN during change attempt.");
            ErrorMessage = "Current PIN is incorrect.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(newPin))
        {
            ErrorMessage = "Please enter a new PIN.";
            return false;
        }

        if (newPin.Length < 4)
        {
            ErrorMessage = "PIN must be at least 4 digits.";
            return false;
        }

        if (newPin != confirmPin)
        {
            ErrorMessage = "New PINs do not match.";
            return false;
        }

        if (_securityService.SetPin(newPin))
        {
            _logger?.Write(AppLogLevel.Info, "PinPromptDialogVM", "PIN changed successfully.");
            _securityService.SetPinLockEnabled(true);
            Success = true;
            WasPinUpdated = true;
            CloseDialog(true);
            return true;
        }

        _logger?.Write(AppLogLevel.Error, "PinPromptDialogVM", "Failed to update PIN.");
        ErrorMessage = "Failed to update PIN. Please try again.";
        return false;
    }

    // Triggers Windows Administrator credential elevation
    private async Task ExecuteAdminOverrideAsync()
    {
        try
        {
            IsBusy = true;
            BusyMessage = "Verifying admin credentials…";
            ErrorMessage = string.Empty;

            _logger?.Write(AppLogLevel.Info, "PinPromptDialogVM", "User triggered Admin elevation override.");

            var isAdmin = await _securityService.VerifyAdminCredentialsAsync();

            _logger?.Write(AppLogLevel.Info, "PinPromptDialogVM", $"Admin elevation check completed. isAdmin={isAdmin}");

            if (isAdmin)
            {
                _logger?.Write(AppLogLevel.Info, "PinPromptDialogVM", "Windows Admin elevation verified successfully. Switching to AdminVerified mode.");
                Mode = PinDialogMode.AdminVerified;
            }
            else
            {
                ErrorMessage = "Administrator elevation was not confirmed or was cancelled.";
            }
        }
        catch (Exception ex)
        {
            _logger?.Write(AppLogLevel.Error, "PinPromptDialogVM", $"Admin elevation error: {ex.Message}", ex);
            ErrorMessage = "Admin verification failed. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Admin option: unlock edit mode for current session without modifying stored PIN
    private void ExecuteAdminUnlockOnly()
    {
        _logger?.Write(AppLogLevel.Info, "PinPromptDialogVM", "Admin option selected: Session unlock only.");
        Success = true;
        WasAdminUnlocked = true;
        CloseDialog(true);
    }

    // Admin option: remove PIN and disable requirement
    private void ExecuteAdminDisablePin()
    {
        _logger?.Write(AppLogLevel.Info, "PinPromptDialogVM", "Admin option selected: Remove and disable PIN.");
        _securityService.ClearPin();
        Success = true;
        WasPinCleared = true;
        CloseDialog(true);
    }

    private void CloseDialog(bool success)
    {
        Success = success;
        RequestClose?.Invoke();
    }
}
