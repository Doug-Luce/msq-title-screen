using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.Sheets;

namespace MsqTitleScreen;

/// <summary>
/// Every Main Scenario quest in story order, tagged with the expansion it belongs to.
///
/// Order comes from (JournalCategory row, Quest.SortKey): the journal's own categories
/// are already chronological -- 1-2 ARR, 3-5 HW, 6-7 SB, 8-10 ShB, 11-12 EW, 13-15 DT --
/// and SortKey orders within one. Quest.Expansion then partitions the list exactly on
/// the expansion boundaries ("Before the Dawn" is the last exp 0 quest, "Coming to
/// Ishgard" the first exp 1 one), so no quest IDs need hardcoding.
/// </summary>
internal sealed class MsqIndex
{
    internal readonly record struct Entry(uint RowId, byte Expansion, string Name);

    /// <summary>
    /// Quest state, as (questId) -&gt; bool. Injected rather than called directly so the
    /// ordering logic can be exercised against the real sheets without a running client.
    /// </summary>
    internal delegate bool QuestPredicate(ushort questId);

    private readonly QuestPredicate isComplete;
    private readonly QuestPredicate isAccepted;

    /// <summary>Journal sections holding main scenario quests. 1 was added for Dawntrail.</summary>
    private static readonly uint[] MainScenarioSections = [0, 1];

    internal IReadOnlyList<Entry> Quests { get; }

    internal MsqIndex(IEnumerable<Quest> sheet, QuestPredicate isComplete, QuestPredicate isAccepted)
    {
        this.isComplete = isComplete;
        this.isAccepted = isAccepted;

        Quests = sheet
            .Select(q => (quest: q, category: q.JournalGenre.ValueNullable?.JournalCategory.ValueNullable))
            .Where(x => x.category is not null && MainScenarioSections.Contains(x.category.Value.JournalSection.RowId))
            .Select(x => (x.quest, x.category!.Value, name: x.quest.Name.ExtractText()))
            .Where(x => !string.IsNullOrWhiteSpace(x.name))
            .OrderBy(x => x.Value.RowId)
            .ThenBy(x => x.quest.SortKey)
            .Select(x => new Entry(x.quest.RowId, (byte)x.quest.Expansion.RowId, x.name))
            .ToArray();
    }

    /// <summary>
    /// Index of the furthest MSQ quest this character has reached -- accepted or complete --
    /// or -1 for a character that has not started the story. Walks backwards so anyone past
    /// A Realm Reborn stops within a few hundred entries.
    /// </summary>
    internal int FurthestReached()
    {
        for (var i = Quests.Count - 1; i >= 0; i--)
        {
            var id = (ushort)(Quests[i].RowId & 0xFFFF);
            if (isComplete(id) || isAccepted(id))
                return i;
        }

        return -1;
    }

    /// <summary>Has the quest at <paramref name="index"/> been completed, as opposed to merely accepted?</summary>
    internal bool IsComplete(int index) =>
        index >= 0 && isComplete((ushort)(Quests[index].RowId & 0xFFFF));

    /// <summary>
    /// Which expansion's title screen this character's progress calls for.
    /// <paramref name="rollOver"/> mirrors what the game does on its own: finishing an
    /// expansion's last MSQ quest moves you to the next title screen right away, rather
    /// than waiting for the first quest of the next expansion to be picked up.
    /// </summary>
    internal byte DesiredExpansion(bool rollOver, out int questIndex)
    {
        questIndex = FurthestReached();
        if (questIndex < 0)
            return 0;

        var expansion = Quests[questIndex].Expansion;
        if (rollOver
            && IsComplete(questIndex)
            && questIndex + 1 < Quests.Count
            && Quests[questIndex + 1].Expansion > expansion)
            expansion = Quests[questIndex + 1].Expansion;

        return expansion;
    }
}
