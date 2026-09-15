using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.MVVM.Model;

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
            _logger?.Write(AppLogLevel.Error, nameof(SecurityService), $"Failed to save PIN: {ex.Message}", ex);
            return false;
        }
    }

    // Enables or disables the requirement for PIN when toggling Edit Mode.
    // When disabling, the stored PIN is cleared so re-enabling will prompt for a new PIN.
    public bool SetPinLockEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(SecurityRegistryPath, writable: true);
            key.SetValue(RequirePinValue, enabled ? 1 : 0, RegistryValueKind.DWord);

            if (!enabled)
            {
                if (key.GetValue(PinDataValue) != null)
                {
                    key.DeleteValue(PinDataValue, false);
                }
                _logger?.Write(AppLogLevel.Info, nameof(SecurityService), "PIN lock disabled and stored PIN cleared from registry.");
            }
            else
            {
                _logger?.Write(AppLogLevel.Info, nameof(SecurityService), $"PIN lock requirement set to: {enabled}");
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Write(AppLogLevel.Error, nameof(SecurityService), $"Failed setting PIN lock enabled status: {ex.Message}", ex);
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
            _logger?.Write(AppLogLevel.Error, nameof(SecurityService), $"Failed verifying PIN: {ex.Message}", ex);
            return false;
        }
    }

    // Clears the saved PIN and disables the PIN lock
    public bool ClearPin()
    {
        return SetPinLockEnabled(false);
    }

    // Triggers a Windows UAC elevation prompt to verify local Windows Administrator credentials
    public async Task<bool> VerifyAdminCredentialsAsync()
    {
        return await Task.Run(() =>
        {
            var token = Guid.NewGuid().ToString("N");
            var tokenFilePath = Path.Combine(Globals.g_LogsDir, $"admin_auth_{token}.flag");
            var eventNameGlobal = $@"Global\ENRL_AdminAuth_{token}";
            var eventNameLocal = $@"Local\ENRL_AdminAuth_{token}";

            EventWaitHandle? eventGlobal = null;
            EventWaitHandle? eventLocal = null;
            Process? process = null;

            try
            {
                try
                {
                    eventGlobal = new EventWaitHandle(false, EventResetMode.ManualReset, eventNameGlobal);
                }
                catch (Exception ex)
                {
                    _logger?.Write(AppLogLevel.Debug, nameof(SecurityService), $"Note: Global event creation skipped ({ex.Message}).");
                }

                try
                {
                    eventLocal = new EventWaitHandle(false, EventResetMode.ManualReset, eventNameLocal);
                }
                catch (Exception ex)
                {
                    _logger?.Write(AppLogLevel.Debug, nameof(SecurityService), $"Note: Local event creation skipped ({ex.Message}).");
                }

                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Process.GetCurrentProcess().MainModule?.FileName;
                }

                _logger?.Write(AppLogLevel.Info, nameof(SecurityService),
                    $"[VerifyAdminCredentialsAsync] Initiating elevation verification. ProcessPath='{exePath}'");

                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    _logger?.Write(AppLogLevel.Error, nameof(SecurityService),
                        $"[VerifyAdminCredentialsAsync] Executable path is null, empty, or file does not exist: '{exePath}'");
                    return false;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--verify-admin {token} \"{tokenFilePath}\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                _logger?.Write(AppLogLevel.Info, nameof(SecurityService),
                    $"[VerifyAdminCredentialsAsync] Launching elevated helper process: FileName='{psi.FileName}', Arguments='{psi.Arguments}', Verb='{psi.Verb}'");

                try
                {
                    process = Process.Start(psi);
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    // Native error code 1223 is ERROR_CANCELLED (user dismissed the UAC prompt)
                    _logger?.Write(AppLogLevel.Info, nameof(SecurityService),
                        "[VerifyAdminCredentialsAsync] Administrator prompt was cancelled by the user (Win32ErrorCode=1223).");
                    return false;
                }
                catch (Win32Exception ex)
                {
                    _logger?.Write(AppLogLevel.Error, nameof(SecurityService),
                        $"[VerifyAdminCredentialsAsync] Win32Exception during admin elevation (NativeErrorCode={ex.NativeErrorCode}): {ex.Message}", ex);
                    return false;
                }
                catch (Exception ex)
                {
                    _logger?.Write(AppLogLevel.Error, nameof(SecurityService),
                        $"[VerifyAdminCredentialsAsync] Error during admin elevation check: {ex.Message}", ex);
                    return false;
                }

                int pid = -1;
                try { pid = process?.Id ?? -1; } catch { /* ignore */ }

                _logger?.Write(AppLogLevel.Info, nameof(SecurityService),
                    $"[VerifyAdminCredentialsAsync] Elevation request dispatched. Process handle returned: {process != null}, PID: {pid}. Awaiting verification response...");

                var sw = Stopwatch.StartNew();
                bool isVerified = false;

                // Wait up to 30 seconds for the elevated helper to signal completion or write receipt
                while (sw.ElapsedMilliseconds < 30000)
                {
                    // 1. Check EventWaitHandles
                    if ((eventGlobal?.WaitOne(200) == true) || (eventLocal?.WaitOne(200) == true))
                    {
                        isVerified = true;
                        _logger?.Write(AppLogLevel.Info, nameof(SecurityService),
                            $"[VerifyAdminCredentialsAsync] Administrator status confirmed via EventWaitHandle @ {sw.ElapsedMilliseconds} ms.");
                        break;
                    }

                    // 2. Check Auth flag file
                    if (File.Exists(tokenFilePath))
                    {
                        try
                        {
                            var content = File.ReadAllText(tokenFilePath);
                            if (content.StartsWith("VERIFIED:", StringComparison.Ordinal))
                            {
                                isVerified = true;
                                _logger?.Write(AppLogLevel.Info, nameof(SecurityService),
                                    $"[VerifyAdminCredentialsAsync] Administrator status confirmed via flag receipt @ {sw.ElapsedMilliseconds} ms: '{content}'.");
                                break;
                            }
                        }
                        catch (IOException)
                        {
                            // Transient file access lock while child writes, will be picked up next iteration
                        }
                    }

                    // 3. Check Process exit code if a process handle was returned
                    if (process != null && process.HasExited)
                    {
                        int exitCode = process.ExitCode;
                        _logger?.Write(AppLogLevel.Info, nameof(SecurityService),
                            $"[VerifyAdminCredentialsAsync] Child process exited with code {exitCode} @ {sw.ElapsedMilliseconds} ms.");
                        if (exitCode == 0)
                        {
                            isVerified = true;
                        }
                        break;
                    }

                    Thread.Sleep(100);
                }

                if (!isVerified && sw.ElapsedMilliseconds >= 30000)
                {
                    _logger?.Write(AppLogLevel.Warning, nameof(SecurityService),
                        "[VerifyAdminCredentialsAsync] Verification timed out after 30 seconds without confirmation.");
                }

                if (process != null && !process.HasExited)
                {
                    try { process.Kill(); } catch { /* ignore */ }
                }

                return isVerified;
            }
            finally
            {
                process?.Dispose();
                eventGlobal?.Dispose();
                eventLocal?.Dispose();

                try
                {
                    if (File.Exists(tokenFilePath))
                    {
                        File.Delete(tokenFilePath);
                    }
                }
                catch { /* ignore */ }
            }
        });
    }
}
