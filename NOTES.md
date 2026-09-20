# MSQ Title Screen -- notes

## The game already has the lever; it is just global

Two system config options do all the work (`FFXIVClientStructs` `ConfigOption`, and
`Dalamud.Game.Config.SystemConfigOption`):

```
MsqProgress      = 157
TitleScreenType  = 159
```

In `~/.xlcore/ffxivConfig/FFXIV.cfg` they show up as plain lines:

```
MsqProgress     37860
TitleScreenType 4294967295      # -1: "decide from MsqProgress"
```

`TitleScreenType` is what the title screen's own **Movies & Title** menu writes, which is
why driving it is enough -- the value is read at boot, long before a character exists, so
no lobby hooking is needed (that is the part TitleEdit does the hard way).

`MsqProgress` is the reason the stock behaviour feels wrong: it is one number for the
whole install, holding the *furthest* progress any character reached. A character partway
through Heavensward still boots into the newest title screen because some other character
went further.

## MsqProgress is compared against ExVersion.MenuScreen

`ExVersion` has a `MenuScreen` column, and its values live in the same number space as
`MsqProgress`:

```
0  A Realm Reborn   MenuScreen=0
1  Heavensward      MenuScreen=8240
2  Stormblood       MenuScreen=16090
3  Shadowbringers   MenuScreen=26070
4  Endwalker        MenuScreen=36530
5  Dawntrail        MenuScreen=38390
```

So `MsqProgress 37860` = past Endwalker's threshold, short of Dawntrail's -> Endwalker
title screen. A scan of every sheet for those five values found them only in `ExVersion`
itself, so whatever produces the number is computed client-side; it is not a quest row
ID, a SortKey, or anything else in the EXD data. Not worth decoding -- writing
`TitleScreenType` outright sidesteps it.

## Ordering the MSQ without hardcoding quest IDs

`Quest` rows carry `JournalGenre` -> `JournalCategory` -> `JournalSection`, and the
main scenario sections are 0 (ARR through EW) and 1 (Dawntrail, added separately).
Category row order is already chronological, and `Quest.SortKey` orders within one:

```
 1 Seventh Umbral Era        Close to Home ............ The Ultimate Weapon
 2 Seventh Astral Era        The Price of Principles .. Before the Dawn
 3 Heavensward               Coming to Ishgard ........ Heavensward
 4 Dragonsong                An Uncertain Future ...... Litany of Peace
 5 Post-Dragonsong           Promises Kept ............ The Far Edge of Fate
 6 Stormblood                Beyond the Great Wall .... Stormblood
 7 Post-Stormblood           Arenvald's Adventure ..... A Requiem for Heroes
 8 Shadowbringers            The Syrcus Trench ........ Shadowbringers
 9 Post-Shadowbringers       Shaken Resolve ........... Reflections in Crystal
10 Post-Shadowbringers II    Alisaie's Quest .......... Death Unto Dawn
11 Endwalker                 The Next Ship to Sail .... Endwalker
12 Post-Endwalker            Newfound Adventure ....... The Coming Dawn
13 Dawntrail                 A New World to Explore ... Dawntrail
14 Post-Dawntrail            A Royal Invitation ....... The Promise of Tomorrow
15 Post-Dawntrail II         With the Winds ........... Windborne
```

1050 quests. `Quest.Expansion` then partitions that list *exactly* on the expansion
boundaries -- "Before the Dawn" is the last `Expansion == 0` quest, "Coming to Ishgard"
the first `Expansion == 1` one -- so the whole mapping is data-driven and survives 8.0
without a code change.

Sheets were checked offline against the installed game data with Lumina, no client
needed: reference `Lumina.dll` / `Lumina.Excel.dll` out of `~/.xlcore/dalamud/Hooks/dev/`
and point `new GameData(...)` at the sqpack folder.

## Still to confirm in game

- That `TitleScreenType` takes 0-5 in `ExVersion` row order. It matches the enum TitleEdit
  uses for the lobby's live value, but the config option has not been checked against the
  Movies & Title menu.
- Whether the client rewrites `TitleScreenType` at login or logout. It should not -- it is
  a user setting -- but `MsqProgress` certainly is rewritten, and 7.15 has a patch note
  about the login path touching this area.
