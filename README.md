# MSQ Title Screen

<img src="images/icon.png" width="128" alt="">

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin that shows the title screen of the
expansion you are actually playing through.

## The problem

FFXIV decides which title screen to show from a single number stored per installation, not
per character: `MsqProgress` in `FFXIV.cfg`, holding the furthest main scenario progress
any character on the PC has reached. Start an alt, or come back to a character partway
through Heavensward, and you still boot into the newest expansion's screen, logo and music
included.

Before patch 7.15 the value drifted between characters, which some people liked; 7.15
[fixed that as a bug](https://forum.square-enix.com/ffxiv/threads/513935), and the
[feature request](https://forum.square-enix.com/ffxiv/threads/513935) to bring it back as
an option has gone unanswered. The title screen's **Movies & Title** menu can pin a screen
by hand, but it does not follow anything.

## What this does

On login, and once a minute while you play, the plugin finds the furthest main scenario
quest your current character has accepted or completed, and points the game's own title
screen setting at that expansion. The next time you reach the title screen you get that
expansion's background, logo and music.

Nothing is hooked, patched or modded: the plugin writes `TitleScreenType`, the same
setting the Movies & Title menu writes. Turn the plugin off, or press **Hand back to the
game**, and the setting goes back to the game's automatic behaviour.

The title screen is drawn before any character is known, so what you see is the story
position of whoever played last. That is as close to per-character as the game allows.

## Install

Add this URL to Dalamud's custom plugin repositories (`/xlsettings` -> Experimental ->
Custom Plugin Repositories), then install **MSQ Title Screen** from the plugin installer:

```
https://raw.githubusercontent.com/Doug-Luce/msq-title-screen/main/repo.json
```

## Commands

| Command | What it does |
| --- | --- |
| `/msqtitle` | Which quest you are on, which title screen that maps to, and what the setting currently is |
| `/msqtitle config` | Settings window |
| `/msqtitle reset` | Hand the setting back to the game |

## Settings

- **Follow the played character's story position** - the plugin's on/off switch.
- **Advance on finishing an expansion, not on starting the next** - with this on, clearing
  an expansion's last quest ("Before the Dawn", "The Coming Dawn") moves you to the next
  title screen immediately, which is what the game does on its own. With it off, the screen
  changes when you accept the first quest of the next expansion.
- **Say so in chat when the setting changes**.

## How the story position is worked out

No quest IDs are hardcoded. Main scenario quests are collected from the journal's own
structure - `Quest` -> `JournalGenre` -> `JournalCategory` -> `JournalSection`, sections 0
and 1 - and ordered by (category, `SortKey`), which is already chronological. `Quest.Expansion`
then partitions that list exactly on the expansion boundaries. A new expansion needs no
code change.

`NOTES.md` has the full reverse-engineering trail, including why driving a config option
beats hooking the lobby.

## Building

```sh
./deploy.sh                 # build and drop into ~/.xlcore/devPlugins
```

Builds against the Dalamud assemblies already on disk (`~/.xlcore/dalamud/Hooks/dev/`);
override with `-p:DalamudLibPath=...`.

## Tests

```sh
dotnet run --project tests/MsqIndexTests -c Release
```

Runs the plugin's own quest-ordering code against the installed game data with simulated
quest state - structure, named story anchors, expansion boundaries, the roll-over rule, and
characters with story-skip holes. No game client required.

## Credits

[TitleEdit](https://github.com/RokasKil/TitleEdit) does the much harder job of replacing
title and character select screens wholesale. This plugin only moves the vanilla setting.
