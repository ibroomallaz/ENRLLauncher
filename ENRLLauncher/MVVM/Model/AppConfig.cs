using System.Collections.Generic;

namespace ENRLLauncher.MVVM.Model;

public class AppConfig
{
    public string PreferredStartupMode { get; set; } = "Maximized";
    public bool AlwaysOnTopInCompact { get; set; }
    public bool AutoCloseOnLaunch { get; set; }
    public double WindowWidth { get; set; } = 1080;
    public double WindowHeight { get; set; } = 720;
    public double? WindowTop { get; set; }
    public double? WindowLeft { get; set; }
    public List<LaunchItem> LaunchItems { get; set; } = [];
}