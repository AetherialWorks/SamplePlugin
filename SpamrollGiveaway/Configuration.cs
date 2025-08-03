using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace SpamrollGiveaway;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public List<int> WinningNumbers { get; set; } = new();
    public int RollTimeout { get; set; } = 30;
    public bool DebugMode { get; set; } = false;
    public string LocalPlayerName { get; set; } = "";
    public bool AutoCloseAfterWin { get; set; } = false;
    public bool ShowRollHistory { get; set; } = true;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        pluginInterface!.SavePluginConfig(this);
    }
}
