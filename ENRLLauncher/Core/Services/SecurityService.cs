using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;

namespace ENRLLauncher.Core.Services;

public class SecurityService : ISecurityService
{
    private const string SecurityRegistryPath = @"Software\UArizona\ENRLLauncher\Security";
    private const string RequirePinValue = "RequireEditModePin";
    private const string PinDataValue = "EditModePinData";

    // Application-specific entropy byte array for DPAPI
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("UArizona.ENRL.PinEntropy.v1");

    private readonly IAppLogger? _logger;

    public SecurityService(IAppLogger? logger = null)
    {
        _logger = logger;
    }

    // Indicates whether the Edit Mode PIN lock is currently enabled
    public bool IsPinLockEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(SecurityRegistryPath, writable: false);
                var val = key?.GetValue(RequirePinValue);
                return val is int i && i == 1;
            }
            catch (Exception ex)
            {
                _logger?.Write(AppLogLevel.Error, nameof(SecurityService), $"Error reading PIN lock status: {ex.Message}");
                return false;
            }
        }
    }

    // Indicates whether an encrypted PIN exists in the registry
    public bool HasPinSet
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(SecurityRegistryPath, writable: false);
                return key?.GetValue(PinDataValue) is byte[] bytes && bytes.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    // Stores the specified PIN securely via DPAPI into the registry and enables PIN lock
    public bool SetPin(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin)) return false;

        try
        {
            var pinBytes = Encoding.UTF8.GetBytes(pin);
            var encrypted = ProtectedData.Protect(pinBytes, Entropy, DataProtectionScope.CurrentUser);

            using var key = Registry.CurrentUser.CreateSubKey(SecurityRegistryPath, writable: true);
            key.SetValue(PinDataValue, encrypted, RegistryValueKind.Binary);
            key.SetValue(RequirePinValue, 1, RegistryValueKind.DWord);

            _logger?.Write(AppLogLevel.Info, nameof(SecurityService), "New Edit Mode PIN successfully encrypted and stored.");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Write(AppLogLevel.Error, nameof(SecurityService), $"Failed to save PIN: {ex.Message}");
            return false;
        }
    }

    // Enables or disables the requirement for PIN when toggling Edit Mode
    public bool SetPinLockEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(SecurityRegistryPath, writable: true);
            key.SetValue(RequirePinValue, enabled ? 1 : 0, RegistryValueKind.DWord);
            _logger?.Write(AppLogLevel.Info, nameof(SecurityService), $"PIN lock requirement set to: {enabled}");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Write(AppLogLevel.Error, nameof(SecurityService), $"Failed setting PIN lock enabled status: {ex.Message}");
            return false;
        }
    }

    // Verifies if the provided PIN matches the saved PIN
    public bool VerifyPin(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin)) return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SecurityRegistryPath, writable: false);
            if (key?.GetValue(PinDataValue) is not byte[] encrypted || encrypted.Length == 0)
                return false;

            var decryptedBytes = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            var savedPin = Encoding.UTF8.GetString(decryptedBytes);
            return string.Equals(pin, savedPin, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            _logger?.Write(AppLogLevel.Error, nameof(SecurityService), $"Failed verifying PIN: {ex.Message}");
            return false;
        }
    }

    // Clears the saved PIN and disables the PIN lock
    public bool ClearPin()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(SecurityRegistryPath, writable: true);
            key.SetValue(RequirePinValue, 0, RegistryValueKind.DWord);
            if (key.GetValue(PinDataValue) != null)
            {
                key.DeleteValue(PinDataValue, false);
            }
            _logger?.Write(AppLogLevel.Info, nameof(SecurityService), "Edit Mode PIN cleared and disabled.");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Write(AppLogLevel.Error, nameof(SecurityService), $"Failed clearing PIN: {ex.Message}");
            return false;
        }
    }

    // Triggers a Windows UAC elevation prompt to verify local Windows Administrator credentials
    public async Task<bool> VerifyAdminCredentialsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Process.GetCurrentProcess().MainModule?.FileName;
                }

                if (string.IsNullOrEmpty(exePath)) return false;

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--verify-admin",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                // Wait up to 60 seconds for the administrator to respond to the UAC prompt
                process.WaitForExit(60000);
                return process.ExitCode == 0;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // Native error code 1223 is ERROR_CANCELLED (user dismissed the UAC prompt)
                _logger?.Write(AppLogLevel.Info, nameof(SecurityService), "Administrator prompt was cancelled by the user.");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.Write(AppLogLevel.Error, nameof(SecurityService), $"Error during admin elevation check: {ex.Message}");
                return false;
            }
        });
    }
}
