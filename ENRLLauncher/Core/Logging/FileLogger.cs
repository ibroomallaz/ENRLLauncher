using ENRLLauncher.Core.Enums;
using ENRLLauncher.Core.Interfaces;
using System.IO;
using System.Text;

namespace ENRLLauncher.Core.Logging
{
    // Daily rolling file logger with retention; cleanup is deferred until SetRetentionDays is called.
    public sealed class FileLogger : IAppLogger, IDisposable
    {
        private readonly Lock _sync = new();
        private int _retentionDays;
        private string _dir;
        private DateTime _dayUtc;
        private StreamWriter? _writer;

        // Cleanup is disabled until settings specify retention.
        private bool _cleanupEnabled = false;

        // Expose where logs are written.
        public string DirectoryPath => _dir;

        public FileLogger(string preferredDir, int retentionDays = 14)
        {
            // Use provided retention but DO NOT clean yet; enable only via SetRetentionDays.
            _retentionDays = Math.Max(1, retentionDays);
            _dir = EnsureDirOrFallback(preferredDir);
            _dayUtc = DateTime.UtcNow.Date;
            OpenWriter_NoThrow();
            // Intentionally not calling TryCleanupOldFiles() here to avoid premature culling.
        }

        public void Write(AppLogLevel level, string tag, string message, Exception? ex = null)
        {
            var now = DateTime.UtcNow;

            var sb = new StringBuilder()
                .Append('[').Append(now.ToString("O")).Append("] ")
                .Append('[').Append(level).Append("] ")
                .Append(tag).Append(": ").AppendLine(message);

            if (ex != null) sb.AppendLine(ex.ToString());

            lock (_sync)
            {
                if (now.Date != _dayUtc)
                {
                    _dayUtc = now.Date;
                    ReopenForNewDay_NoThrow();

                    // Only clean after settings enabled cleanup.
                    if (_cleanupEnabled) TryCleanupOldFiles_NoThrow();
                }

                try
                {
                    _writer!.Write(sb.ToString());
                    _writer!.Flush();
                }
                catch
                {
                    // Repair directory and retry once.
                    _dir = EnsureDirOrFallback(_dir);
                    ReopenForNewDay_NoThrow();
                    TryWriteFallback(sb.ToString());
                }
            }
        }

        // Called by Log.ApplySettings; enables cleanup and performs a first pass immediately.
        public void SetRetentionDays(int days)
        {
            lock (_sync)
            {
                _retentionDays = Math.Max(1, days);
                _cleanupEnabled = true;
            }
            TryCleanupOldFiles(); // First cleanup now that we have the real retention.
        }

        // Empties today's file in place; keeps handle open.
        public void TruncateToday()
        {
            lock (_sync)
            {
                try
                {
                    _writer?.Flush();
                    if (_writer?.BaseStream is FileStream fs)
                    {
                        fs.SetLength(0);
                        fs.Seek(0, SeekOrigin.Begin);
                    }
                }
                catch { }
            }
        }

        // Deletes logs older than cutoff (UTC date).
        public void PurgeOlderThan(DateTime cutoffUtc)
        {
            try
            {
                foreach (var f in System.IO.Directory.EnumerateFiles(_dir, "app-*.log"))
                {
                    var d = ParseDate(Path.GetFileNameWithoutExtension(f)); // app-YYYYMMDD
                    if (d.HasValue && d.Value < cutoffUtc.Date)
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
            }
            catch { }
        }

        // Deletes all logs; optionally also clears today's file.
        public void PurgeAll(bool includeToday = false)
        {
            try
            {
                var today = Path.Combine(_dir, $"app-{_dayUtc:yyyyMMdd}.log");
                foreach (var f in System.IO.Directory.EnumerateFiles(_dir, "app-*.log"))
                {
                    if (!includeToday && string.Equals(f, today, StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.Delete(f); } catch { }
                }
                if (includeToday) TruncateToday();
            }
            catch { }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                try { _writer?.Dispose(); } catch { }
                _writer = null;
            }
        }

        private static string EnsureDirOrFallback(string preferred)
        {
            try
            {
                System.IO.Directory.CreateDirectory(preferred);
                return preferred;
            }
            catch
            {
                var temp = Path.Combine(Path.GetTempPath(), "UArizona", "ENRLLauncher", "logs");
                System.IO.Directory.CreateDirectory(temp);
                return temp;
            }
        }

        private void OpenWriter_NoThrow()
        {
            lock (_sync) { ReopenForNewDay_NoThrow(); }
        }

        private void ReopenForNewDay_NoThrow()
        {
            try { _writer?.Dispose(); } catch { }
            try
            {
                var path = Path.Combine(_dir, $"app-{_dayUtc:yyyyMMdd}.log");
                _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8)
                {
                    AutoFlush = true
                };
            }
            catch { }
        }

        private void TryCleanupOldFiles()
        {
            try { TryCleanupOldFiles_NoThrow(); } catch { }
        }

        private void TryCleanupOldFiles_NoThrow()
        {
            var cutoff = DateTime.UtcNow.Date.AddDays(-_retentionDays);
            foreach (var f in System.IO.Directory.EnumerateFiles(_dir, "app-*.log"))
            {
                var d = ParseDate(Path.GetFileNameWithoutExtension(f));
                if (d.HasValue && d.Value < cutoff)
                {
                    try { File.Delete(f); } catch { }
                }
            }
        }

        private static DateTime? ParseDate(string name) // "app-YYYYMMDD"
        {
            if (!string.IsNullOrEmpty(name) && name.Length >= 12)
            {
                var datePart = name[4..];
                if (DateTime.TryParseExact(
                        datePart, "yyyyMMdd", null,
                        System.Globalization.DateTimeStyles.AssumeUniversal,
                        out var d))
                {
                    return d.Date;
                }
            }
            return null;
        }

        private void TryWriteFallback(string line)
        {
            try
            {
                var path = Path.Combine(_dir, $"app-{_dayUtc:yyyyMMdd}.log");
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch { }
        }
    }
}