using System.Threading.Tasks;
using ENRLLauncher.MVVM.Model;

namespace ENRLLauncher.Core.Interfaces;

public interface ILauncherService
{
    bool CanLaunch(LaunchItem item);
    Task<bool> LaunchAsync(LaunchItem item);
}