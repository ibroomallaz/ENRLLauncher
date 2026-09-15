namespace ENRLLauncher.Core.Interfaces;

public interface ISecurityService
{
    // Indicates whether the Edit Mode PIN lock is currently enabled
    bool IsPinLockEnabled { get; }

    // Indicates whether a PIN has been saved in the registry
    bool HasPinSet { get; }

    // Stores the specified PIN securely via DPAPI into the registry and enables PIN lock
    bool SetPin(string pin);

    // Enables or disables the requirement for PIN when toggling Edit Mode
    bool SetPinLockEnabled(bool enabled);

    // Verifies if the provided PIN matches the saved PIN
    bool VerifyPin(string pin);

    // Clears the saved PIN and disables the PIN lock
    bool ClearPin();

    // Triggers a Windows UAC elevation prompt to verify local Windows Administrator credentials
    Task<bool> VerifyAdminCredentialsAsync();
}
