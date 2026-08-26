using ENRLLauncher.Core.Enums;

namespace ENRLLauncher.Core.Interfaces
{
    // Minimal logging contract; implementations handle the actual sink.
    public interface IAppLogger
    {
        void Write(AppLogLevel level, string tag, string message, Exception? ex = null);
    }
}