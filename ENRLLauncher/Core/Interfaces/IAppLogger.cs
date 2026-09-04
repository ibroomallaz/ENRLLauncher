using System;
using ENRLLauncher.Core.Enums;

namespace ENRLLauncher.Core.Interfaces;

public interface IAppLogger
{
    // Core sink required by implementations
    void Write(AppLogLevel level, string tag, string message, Exception? ex = null);

    // General Log overloads (satisfies existing ViewModel calls)
    void Log(AppLogLevel level, string message, Exception? ex = null) =>
        Write(level, "General", message, ex);

    void Log(AppLogLevel level, string tag, string message, Exception? ex = null) =>
        Write(level, tag, message, ex);

    // Convenience level helpers
    void Info(string message) =>
        Write(AppLogLevel.Info, "General", message);

    void Info(string tag, string message) =>
        Write(AppLogLevel.Info, tag, message);

    void Warn(string message) =>
        Write(AppLogLevel.Warning, "General", message);

    void Warn(string tag, string message) =>
        Write(AppLogLevel.Warning, tag, message);

    void Error(string message, Exception? ex = null) =>
        Write(AppLogLevel.Error, "General", message, ex);

    void Error(string tag, string message, Exception? ex = null) =>
        Write(AppLogLevel.Error, tag, message, ex);
}