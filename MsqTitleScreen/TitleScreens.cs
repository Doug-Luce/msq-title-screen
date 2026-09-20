namespace MsqTitleScreen;

/// <summary>
/// Values of the game's own TitleScreenType setting (System config option 159, the one
/// the title screen's "Movies &amp; Title" menu writes). uint.MaxValue -- -1 in the cfg
/// file -- is the stock "follow MsqProgress" behaviour.
/// </summary>
internal static class TitleScreens
{
    internal const uint GameDefault = uint.MaxValue;

    internal static readonly string[] Names =
    [
        "A Realm Reborn",
        "Heavensward",
        "Stormblood",
        "Shadowbringers",
        "Endwalker",
        "Dawntrail",
    ];

    internal static string Describe(uint value) =>
        value == GameDefault ? "game default (furthest progress on this PC)"
        : value < Names.Length ? Names[value]
        : $"unknown ({value})";
}
