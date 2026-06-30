using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;

namespace HazardOverhaul.Builders;

/// <summary>
/// Fluent state-accumulator for Starfield LVLI (LeveledItem) records.
/// Nothing is created or mutated until Build() or Apply() is called:
/// all configuration calls just populate internal fields.
///
///   Build(mod, editorId) -> creates a brand new LeveledItem in the mod and
///                           writes the accumulated state onto it.
///   Apply(existingRecord) -> writes the accumulated state onto an already-existing
///                            LeveledItem (e.g. one resolved from the load order via
///                            GetOrAddAsOverride), leaving its EditorID/FormKey untouched.
///
/// Field reference (Mutagen.Bethesda.Starfield.LeveledItem):
///   ChanceNone   -> float, "Chance None" in CK (0-100 in CK UI, stored as 0.0-100.0 float here)
///   MaxCount     -> byte?, rarely used outside vanilla "use count" lists
///   Flags        -> LeveledItem.Flag [Flags] enum:
///                     CalculateFromAllLevelsLessThanOrEqualPlayer (0x01)
///                     CalculateForEachItemInCount                 (0x02)
///                     UseAll                                      (0x04)
///                     ShowAsMarker1 / ShowAsMarker2                (0x08 / 0x10)
///                     EvalAsStack                                  (0x20)
///                     GetChanceFromRequiredBiome                   (0x80)
///                     DoAllBeforeRepeating                         (0x100)
///   Entries      -> ExtendedList<LeveledItemEntry>, each entry has:
///                     Level     (Int16)  - the level threshold for this entry
///                     Reference (IFormLink<IItemGetter>) - the item/list to award
///                     Count     (Int16)  - how many copies if this entry is picked
///                     ChanceNone(Percent) - per-entry chance to award nothing
/// </summary>
public sealed class LeveledItemBuilder
{
    private readonly List<LeveledItemEntry> listEntries = new();
    private float? chanceNone;
    private byte? maxCount;
    private LeveledItem.Flag listFlags = default;

    private LeveledItemBuilder()
    {
    }

    /// <summary>Starts a new, empty builder. No record exists yet.</summary>
    public static LeveledItemBuilder Create() => new();

    // ---------------------------------------------------------------------
    // Entries
    // ---------------------------------------------------------------------

    /// <summary>
    /// Adds one entry to the accumulated state. <paramref name="count"/> defaults to 1.
    /// <paramref name="chanceNonePercent"/> is 0-100 and defaults to 0 (always awarded if picked).
    /// </summary>
    public LeveledItemBuilder AddEntry(
        IFormLink<IItemGetter> item,
        short level = 1,
        short count = 1,
        float chanceNonePercent = 0f)
    {
        listEntries.Add(new LeveledItemEntry
        {
            Level = level,
            Reference = item,
            Count = count,
            ChanceNone = Percent.FactoryPutInRange(chanceNonePercent / 100f),
        });

        return this;
    }

    /// <summary>
    /// Typed overload for Ingestible records (chems, food, drink) so callers don't have
    /// to manually upcast to IItemGetter via ToLinkGetter&lt;IItemGetter&gt;().
    /// </summary>
    public LeveledItemBuilder AddEntry(
        IIngestibleGetter ingestible,
        short level = 1,
        short count = 1,
        float chanceNonePercent = 0f)
        => AddEntry(ingestible.ToLink<IItemGetter>(), level, count, chanceNonePercent);

    /// <summary>
    /// Typed overload for MiscItem records (loose loot, junk, quest items) so callers don't
    /// have to manually upcast to IItemGetter via ToLinkGetter&lt;IItemGetter&gt;().
    /// </summary>
    public LeveledItemBuilder AddEntry(
        IMiscItemGetter miscItem,
        short level = 1,
        short count = 1,
        float chanceNonePercent = 0f)
        => AddEntry(miscItem.ToLink<IItemGetter>(), level, count, chanceNonePercent);

    /// <summary>
    /// Convenience overload for nesting another leveled list as an entry
    /// (e.g. a per-tier hazard-mod sublist feeding into a master loot list).
    /// </summary>
    public LeveledItemBuilder AddEntry(
        ILeveledItemGetter nestedList,
        short level = 1,
        short count = 1,
        float chanceNonePercent = 0f)
        => AddEntry(nestedList.ToLink<IItemGetter>(), level, count, chanceNonePercent);

    // ---------------------------------------------------------------------
    // List-level settings
    // ---------------------------------------------------------------------

    /// <summary>
    /// Sets the overall "Chance None" for the list (0-100), i.e. the chance the
    /// entire list resolves to nothing regardless of which entry would've been picked.
    /// </summary>
    public LeveledItemBuilder WithChanceNone(float percent)
    {
        chanceNone = percent;
        return this;
    }

    /// <summary>
    /// "Calculate from all levels &lt;= PC's level" checkbox.
    /// On: every entry at or below the relevant level is eligible.
    /// Off: only the entry/entries closest to (without exceeding) that level are eligible.
    /// </summary>
    public LeveledItemBuilder CalculateFromAllLevels(bool enabled = true)
        => SetFlag(LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer, enabled);

    /// <summary>
    /// "Calculate for each item in count" checkbox. Only matters when this list is
    /// itself nested as an entry inside another leveled list whose entry Count > 1.
    /// On:  each of the Count copies independently re-rolls this nested list.
    /// Off: this list is rolled once and the result is duplicated Count times.
    /// </summary>
    public LeveledItemBuilder CalculateForEachItemInCount(bool enabled = true)
        => SetFlag(LeveledItem.Flag.CalculateForEachItemInCount, enabled);

    /// <summary>
    /// "Use All" checkbox. When set, every entry in the list is granted
    /// simultaneously; supersedes both of the flags above.
    /// </summary>
    public LeveledItemBuilder UseAll(bool enabled = true)
        => SetFlag(LeveledItem.Flag.UseAll, enabled);

    /// <summary>
    /// "Calculate for each item before repeating" (DoAllBeforeRepeating). Relevant
    /// when MaxCount is set above 1: forces all entries to be used once before any
    /// repeats are allowed.
    /// </summary>
    public LeveledItemBuilder DoAllBeforeRepeating(bool enabled = true)
        => SetFlag(LeveledItem.Flag.DoAllBeforeRepeating, enabled);

    /// <summary>Sets MaxCount (how many distinct entries to draw from the list when resolved).</summary>
    public LeveledItemBuilder WithMaxCount(byte? maxCount)
    {
        this.maxCount = maxCount;
        return this;
    }

    /// <summary>Escape hatch for setting raw flag bits directly if a future flag isn't wrapped above.</summary>
    public LeveledItemBuilder SetFlag(LeveledItem.Flag flag, bool enabled)
    {
        listFlags = enabled
            ? listFlags | flag
            : listFlags & ~flag;
        return this;
    }

    // ---------------------------------------------------------------------
    // Finalize
    // ---------------------------------------------------------------------

    /// <summary>
    /// Creates a brand new LeveledItem record in <paramref name="mod"/> under
    /// <paramref name="editorId"/> and writes the accumulated state onto it.
    /// </summary>
    public LeveledItem Build(IStarfieldMod mod, string editorId)
    {
        var record = mod.LeveledItems.AddNew(editorId);
        Apply(record);
        return record;
    }

    /// <summary>
    /// Writes the accumulated state onto an already-existing LeveledItem record,
    /// overwriting its Entries, ChanceNone, MaxCount, and Flags. EditorID/FormKey
    /// are left untouched. Useful for patching a vanilla or another mod's leveled
    /// list (e.g. via mod.LeveledItems.GetOrAddAsOverride(existingGetter)) rather
    /// than creating a new one.
    /// </summary>
    public void Apply(LeveledItem record)
    {
        record.Entries ??= new ExtendedList<LeveledItemEntry>();
        record.Entries.AddRange(listEntries);

        if (chanceNone.HasValue)
        {
            record.ChanceNone = chanceNone.Value;
        }

        if (maxCount.HasValue)
        {
            record.MaxCount = maxCount.Value;
        }

        record.Flags = listFlags;
    }
}
