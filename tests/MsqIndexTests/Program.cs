using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lumina;
using Lumina.Excel.Sheets;
using MsqTitleScreen;

// Exercises the plugin's quest ordering against the installed game data. No client
// needed: quest state is simulated, the sheets are real, and MsqIndex.cs is the same
// file the plugin ships.

var sqpack = args.FirstOrDefault() ?? FindGamePath();
if (sqpack is null || !Directory.Exists(sqpack))
{
    Console.Error.WriteLine("Could not find the game's sqpack folder. Pass it as the first argument.");
    return 2;
}

Console.WriteLine($"game data: {sqpack}");
var sheet = new GameData(sqpack).GetExcelSheet<Quest>();
if (sheet is null)
{
    Console.Error.WriteLine("The Quest sheet is missing from that game data.");
    return 2;
}

var quests = sheet.ToList();

var failures = 0;
void Check(string name, bool ok, string detail = "")
{
    Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? $"  [{detail}]" : "")}");
    if (!ok) failures++;
}

// A character who has completed everything up to and including `through`, and accepted
// nothing further. `accepted` adds a single in-progress quest on top.
MsqIndex Character(IReadOnlyList<MsqIndex.Entry> order, int through, int? accepted = null)
{
    var complete = order.Take(through + 1).Select(e => (ushort)(e.RowId & 0xFFFF)).ToHashSet();
    var open = accepted is int a ? (ushort)(order[a].RowId & 0xFFFF) : (ushort)0;
    return new MsqIndex(quests, id => complete.Contains(id), id => open != 0 && id == open);
}

var reference = new MsqIndex(quests, _ => false, _ => false);
var list = reference.Quests;

Console.WriteLine($"\nstructure ({list.Count} main scenario quests)");
Check("list is non-trivial", list.Count > 1000, $"{list.Count}");
Check("expansions never go backwards along the list",
    list.Zip(list.Skip(1)).All(p => p.Second.Expansion >= p.First.Expansion),
    string.Join(" ", list.Select(q => q.Expansion).Distinct()));
Check("every expansion 0-5 is represented",
    Enumerable.Range(0, 6).All(e => list.Any(q => q.Expansion == e)));
Check("no duplicate quests", list.Select(q => q.RowId).Distinct().Count() == list.Count);

// Named anchors, so a future patch reshuffling the journal shows up as a failure here
// rather than as a wrong title screen.
var anchors = new (string Name, byte Expansion)[]
{
    ("The Ultimate Weapon", 0),
    ("Before the Dawn", 0),
    ("Coming to Ishgard", 1),
    ("Heavensward", 1),
    ("The Far Edge of Fate", 1),
    ("Stormblood", 2),
    ("Shadowbringers", 3),
    ("Death Unto Dawn", 3),
    ("Endwalker", 4),
    ("The Coming Dawn", 4),
    ("Dawntrail", 5),
};

Console.WriteLine("\nanchors");
foreach (var (name, expansion) in anchors)
{
    var idx = IndexOf(name);
    Check($"'{name}' is expansion {expansion}", idx >= 0 && list[idx].Expansion == expansion,
        idx >= 0 ? $"found at {idx}, expansion {list[idx].Expansion}" : "not found");
}

Console.WriteLine("\nboundaries");
Check("'Before the Dawn' is the last A Realm Reborn quest",
    list[IndexOf("Before the Dawn") + 1].Expansion == 1);
Check("'Coming to Ishgard' follows it", list[IndexOf("Before the Dawn") + 1].Name == "Coming to Ishgard");
Check("'The Coming Dawn' is the last Endwalker quest",
    list[IndexOf("The Coming Dawn") + 1].Expansion == 5);

Console.WriteLine("\ndetection");
var noProgress = new MsqIndex(quests, _ => false, _ => false);
noProgress.DesiredExpansion(true, out var emptyIdx);
Check("a fresh character reports no progress", emptyIdx == -1);

// Mid-expansion: partway through Heavensward proper.
var midHw = IndexOf("Heavensward") - 5;
Check("mid-Heavensward -> Heavensward", Character(list, midHw).DesiredExpansion(true, out _) == 1,
    $"quest '{list[midHw].Name}'");

// The roll-over rule: finishing 2.55 should move you on, as the game does.
var beforeTheDawn = IndexOf("Before the Dawn");
Check("just finished 'Before the Dawn', roll-over on -> Heavensward",
    Character(list, beforeTheDawn).DesiredExpansion(true, out _) == 1);
Check("just finished 'Before the Dawn', roll-over off -> A Realm Reborn",
    Character(list, beforeTheDawn).DesiredExpansion(false, out _) == 0);
Check("mid-A Realm Reborn is unaffected by roll-over",
    Character(list, IndexOf("The Ultimate Weapon")).DesiredExpansion(true, out _) == 0);

// Accepting the next expansion's first quest counts even when nothing of it is done.
Check("accepted 'Coming to Ishgard' -> Heavensward",
    Character(list, beforeTheDawn - 1, beforeTheDawn + 1).DesiredExpansion(false, out _) == 1);

Check("finished the whole story -> Dawntrail",
    Character(list, list.Count - 1).DesiredExpansion(true, out _) == 5);
Check("no roll-over past the end of the list",
    Character(list, list.Count - 1).DesiredExpansion(true, out _) == 5);

// Story skip potions leave holes: progress must be read from the furthest quest, not
// from an unbroken run at the start.
var skipped = new HashSet<ushort> { (ushort)(list[IndexOf("Endwalker")].RowId & 0xFFFF) };
Check("a character with only a late quest complete -> Endwalker",
    new MsqIndex(quests, id => skipped.Contains(id), _ => false).DesiredExpansion(false, out _) == 4);

Console.WriteLine($"\n{(failures == 0 ? "all checks passed" : $"{failures} check(s) failed")}");
return failures == 0 ? 0 : 1;

int IndexOf(string name)
{
    for (var i = 0; i < list.Count; i++)
        if (list[i].Name == name)
            return i;
    return -1;
}

static string? FindGamePath()
{
    var ini = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".xlcore", "launcher.ini");
    if (!File.Exists(ini))
        return null;

    var line = File.ReadLines(ini).FirstOrDefault(l => l.StartsWith("GamePath=", StringComparison.OrdinalIgnoreCase));
    return line is null ? null : Path.Combine(line["GamePath=".Length..].Trim(), "game", "sqpack");
}
