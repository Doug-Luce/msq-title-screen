using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;

namespace MsqTitleScreen;

internal sealed class ConfigWindow
{
    private const string KoFi = "https://ko-fi.com/aurelius1";

    private readonly Configuration cfg;
    private readonly Plugin plugin;

    internal bool IsOpen;

    internal ConfigWindow(Configuration cfg, Plugin plugin)
    {
        this.cfg = cfg;
        this.plugin = plugin;
    }

    internal void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.SetNextWindowSize(new Vector2(460, 0), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("MSQ Title Screen", ref IsOpen))
        {
            ImGui.End();
            return;
        }

        var enabled = cfg.Enabled;
        if (ImGui.Checkbox("Follow the played character's story position", ref enabled))
        {
            cfg.Enabled = enabled;
            cfg.Save();
            if (enabled)
                plugin.Apply();
        }

        var rollOver = cfg.RollOverOnExpansionComplete;
        if (ImGui.Checkbox("Advance on finishing an expansion, not on starting the next", ref rollOver))
        {
            cfg.RollOverOnExpansionComplete = rollOver;
            cfg.Save();
            plugin.Apply();
        }

        var announce = cfg.AnnounceChanges;
        if (ImGui.Checkbox("Say so in chat when the setting changes", ref announce))
        {
            cfg.AnnounceChanges = announce;
            cfg.Save();
        }

        ImGui.Spacing();
        ImGui.TextWrapped(plugin.StatusLine());
        ImGui.Spacing();

        if (ImGui.Button("Re-check now"))
            plugin.Apply(verbose: true);

        ImGui.SameLine();
        if (ImGui.Button("Hand back to the game"))
            plugin.RestoreGameDefault();

        ImGui.Spacing();
        ImGui.TextDisabled("The title screen is drawn before any character is known, so it\nshows the story position of whoever played last.");

        ImGui.Separator();
        if (ImGui.Button("Support on Ko-fi"))
            Util.OpenLink(KoFi);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(KoFi);

        ImGui.End();
    }
}
