using ENRLLauncher.MVVM.Model;

namespace ENRLLauncher.Core.Interfaces;

public interface ILayoutService
{
    Task<List<LaunchItem>> LoadLayoutAsync();
    Task SaveLayoutAsync(IEnumerable<LaunchItem> items);
}