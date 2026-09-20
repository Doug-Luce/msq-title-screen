using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace MsqTitleScreen;

public sealed unsafe class Plugin : IDalamudPlugin
{
    /// <summary>
    /// System config option 159. Written by the title screen's own "Movies &amp; Title"
    /// menu, and read at boot before any character exists -- which is why the setting,
    /// rather than the lobby scene, is what this plugin drives.
    /// </summary>
    private const string TitleScreenTypeOption = "TitleScreenType";

    /// <summary>Config option 157: the game's own record of how far the story has got.</summary>
    private const string MsqProgressOption = "MsqProgress";

    /// <summary>A full re-check costs a backwards walk over ~1000 quest lookups. Once a minute is plenty.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly Configuration config;
    private readonly ConfigWindow window;

    private MsqIndex? index;
    private DateTime nextPoll = DateTime.MinValue;

    /// <summary>
    /// Set when writing the setting throws. Polling runs on the framework tick, so one
    /// bad write must not become a failure every minute for the rest of the session.
    /// </summary>
    private bool writesDisabled;

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        window = new ConfigWindow(config, this);

        PluginInterface.UiBuilder.Draw += window.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleWindow;
        PluginInterface.UiBuilder.OpenMainUi += ToggleWindow;

        ClientState.Login += OnLogin;
        Framework.Update += OnUpdate;

        Commands.AddHandler("/msqtitle", new CommandInfo(OnCommand)
        {
            HelpMessage = "Show which title screen your story position maps to. /msqtitle config for settings.",
        });

        Apply();
    }

    /// <summary>The quest list is built from the sheets once, on first use rather than at load.</summary>
    private MsqIndex Index => index ??= new MsqIndex(Data.GetExcelSheet<Quest>(), IsQuestComplete, IsQuestAccepted);

    private static bool IsQuestComplete(ushort questId) => QuestManager.IsQuestComplete(questId);

    private static bool IsQuestAccepted(ushort questId)
    {
        var manager = QuestManager.Instance();
        return manager != null && manager->IsQuestAccepted(questId);
    }

    /// <summary>
    /// Point the game's title screen setting at the expansion this character is playing
    /// through. Nothing is written while logged out -- the quest state read below belongs
    /// to whoever is currently logged in, and the saved setting is what the next boot uses.
    /// </summary>
    internal void Apply(bool verbose = false)
    {
        if (!ClientState.IsLoggedIn)
            return;

        if (!config.Enabled)
            return;

        var desired = Index.DesiredExpansion(config.RollOverOnExpansionComplete, out var questIndex);
        if (questIndex < 0)
        {
            Log.Debug("No main scenario progress found for this character; leaving the title screen alone.");
            return;
        }

        if (!GameConfig.System.TryGetUInt(TitleScreenTypeOption, out var current))
        {
            Log.Warning($"Could not read {TitleScreenTypeOption}; is the option still called that?");
            return;
        }

        if (current == desired)
        {
            if (verbose)
                Report($"Title screen already set to {TitleScreens.Describe(desired)}.");
            return;
        }

        if (writesDisabled)
            return;

        try
        {
            GameConfig.System.Set(TitleScreenTypeOption, desired);
        }
        catch (Exception ex)
        {
            writesDisabled = true;
            Log.Error(ex, $"Could not write {TitleScreenTypeOption}; no further attempts this session.");
            Report($"Could not change the title screen setting: {ex.Message}");
            return;
        }

        config.LastWritten = desired;
        config.Save();

        var quest = Index.Quests[questIndex];
        Log.Information($"Title screen {TitleScreens.Describe(current)} -> {TitleScreens.Describe(desired)} (at '{quest.Name}').");
        if (config.AnnounceChanges || verbose)
            Report($"Title screen set to {TitleScreens.Describe(desired)} -- you are on '{quest.Name}'.");
    }

    /// <summary>Hand the setting back to the game, which picks the furthest progress on this PC.</summary>
    internal void RestoreGameDefault()
    {
        try
        {
            GameConfig.System.Set(TitleScreenTypeOption, TitleScreens.GameDefault);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Could not write {TitleScreenTypeOption}.");
            Report($"Could not restore the title screen setting: {ex.Message}");
            return;
        }

        config.LastWritten = TitleScreens.GameDefault;
        config.Save();
        Report("Title screen handed back to the game's own setting.");
    }

    internal string StatusLine()
    {
        if (!ClientState.IsLoggedIn)
            return "Not logged in -- story position is only readable from inside the game.";

        var desired = Index.DesiredExpansion(config.RollOverOnExpansionComplete, out var questIndex);
        if (questIndex < 0)
            return "No main scenario progress found for this character.";

        var quest = Index.Quests[questIndex];
        var state = Index.IsComplete(questIndex) ? "completed" : "in progress";
        GameConfig.System.TryGetUInt(TitleScreenTypeOption, out var current);
        GameConfig.System.TryGetUInt(MsqProgressOption, out var progress);
        return $"'{quest.Name}' ({state}) -> {TitleScreens.Describe(desired)}. "
             + $"Setting is {TitleScreens.Describe(current)}; the game's own MsqProgress is {progress}.";
    }

    private void OnLogin()
    {
        // A new character is a fresh chance for a write that failed earlier.
        writesDisabled = false;
        // The character's quest state is not readable the instant Login fires; the poll
        // below picks it up on the next tick anyway.
        nextPoll = DateTime.MinValue;
    }

    private void OnUpdate(IFramework framework)
    {
        if (DateTime.UtcNow < nextPoll)
            return;

        nextPoll = DateTime.UtcNow + PollInterval;
        Apply();
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "config":
            case "settings":
                ToggleWindow();
                break;
            case "reset":
                RestoreGameDefault();
                break;
            case "":
            case "status":
                Report(StatusLine());
                break;
            default:
                Report("Usage: /msqtitle [status|config|reset]");
                break;
        }
    }

    private void Report(string message) => Chat.Print($"[MSQ Title Screen] {message}");

    private void ToggleWindow() => window.IsOpen = !window.IsOpen;

    public void Dispose()
    {
        Commands.RemoveHandler("/msqtitle");
        Framework.Update -= OnUpdate;
        ClientState.Login -= OnLogin;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleWindow;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleWindow;
        PluginInterface.UiBuilder.Draw -= window.Draw;
    }
}
