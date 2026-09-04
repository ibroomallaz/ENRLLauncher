using System.Threading.Tasks;

namespace ENRLLauncher.Core.Interfaces;

public interface IJsonStorageService
{
    Task<T?> LoadAsync<T>(string filePath) where T : class;
    Task SaveAsync<T>(string filePath, T data, bool atomic = true) where T : class;
    bool Exists(string filePath);
}