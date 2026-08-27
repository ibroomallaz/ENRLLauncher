using System;
using ENRLLauncher.Core.Enums;

namespace ENRLLauncher.MVVM.Model;

public class LaunchItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public string? IconPath { get; set; }
    public LaunchTargetType TargetType { get; set; } = LaunchTargetType.Presentation;
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
}