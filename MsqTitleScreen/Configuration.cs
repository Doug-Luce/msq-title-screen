using Dalamud.Configuration;

namespace MsqTitleScreen;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Whether the title screen is pinned to the played character's story position.</summary>
    public bool Enabled = true;

    /// <summary>
    /// Switch to the next expansion's title screen the moment the previous expansion's
    /// MSQ is finished, instead of when its first quest is accepted. Matches the game's
    /// own behaviour.
    /// </summary>
    public bool RollOverOnExpansionComplete = true;

    /// <summary>Announce in chat when the title screen setting is changed.</summary>
    public bool AnnounceChanges = false;

    /// <summary>Last value written, so the plugin knows what it is responsible for.</summary>
    public uint LastWritten = uint.MaxValue;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
