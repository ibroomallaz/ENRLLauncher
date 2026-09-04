using System.Collections.Generic;
using ENRLLauncher.Core.Interfaces;
using ENRLLauncher.MVVM.Model;
using ENRLLauncher.MVVM.Model.Schema;

namespace ENRLLauncher.Core.Services;

public class LayoutService(IJsonStorageService storageService) : ILayoutService
{
    private readonly IJsonStorageService _storageService = storageService;

    public async Task<List<LaunchItem>> LoadLayoutAsync()
    {
        if (!_storageService.Exists(Globals.g_LayoutPath))
        {
            return [];
        }

        var schema = await _storageService.LoadAsync<LayoutSchema>(Globals.g_LayoutPath);
        if (schema == null)
        {
            return [];
        }

        // Version migration / incompatibility check
        if (schema.SchemaVersion != Globals.g_LayoutSchema)
        {
            // Handle migration logic or archiving if schema bumps in future releases
        }

        return schema.Items?.OrderBy(i => i.SortOrder).ToList() ?? [];
    }

    public async Task SaveLayoutAsync(IEnumerable<LaunchItem> items)
    {
        var schema = new LayoutSchema
        {
            Items = [.. items]
        };

        await _storageService.SaveAsync(Globals.g_LayoutPath, schema, atomic: true);
    }
}