using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Records.Fluent;

/// <summary>
/// Contains spells or perks necessary for a subsystem to have an effect on gameplay.
/// All spells and perks should be applied to the player for the subsystem to work as expected.
/// </summary>
public class RequiredSystemRecords
{
    private IReadOnlyList<ISpellGetter> spells;
    private IReadOnlyList<IPerkGetter> perks;

    public IReadOnlyList<ISpellGetter> Spells { get => spells ?? []; init => spells = [.. value]; }
    public IReadOnlyList<IPerkGetter> Perks { get => perks ?? []; init => perks = [.. value]; }

    public RequiredSystemRecords Union(RequiredSystemRecords other)
    {
        return new RequiredSystemRecords
        {
            Perks = [..Perks,..other.Perks],
            Spells = [..Spells,..other.Spells]
        };
    }
}

public class HazardSystemModEnablerPatcher
{
    private readonly RequiredSystemRecords systemRecords;
    private readonly StarfieldMod outputMod;

    private HazardSystemModEnablerPatcher(RequiredSystemRecords systemRecords, StarfieldMod outputMod)
    {
        this.systemRecords = systemRecords;
        this.outputMod = outputMod;
    }

    public static void WritePatch(RequiredSystemRecords systemRecords,  StarfieldMod outputMod)
    {
        var patcher = new HazardSystemModEnablerPatcher(systemRecords, outputMod);
        patcher.PatchInternal();
    }
    private void PatchInternal()
    {
        var enablerPerk = AddModEnablerPerk(systemRecords.Spells, systemRecords.Perks);
        Console.WriteLine("Enabler Perk: " + enablerPerk.FormKey);
        AddInitQuest(enablerPerk);
    }
    
    private Quest AddInitQuest(Perk enablerPerk)
    {
        var quest = outputMod.Quests.AddNew("Haos_Init_Quest");
        quest.Data = new QuestData()
        {
          Flags = Quest.Flag.StartGameEnabled 
        };
        quest.Name = "Haos Init Quest";

        var playerAlias = CreatePlayerAlias();
        quest.Aliases = [playerAlias];

        ScriptAttachment.OfScript("Haos_PlayerAlias").ApplyTo(playerAlias, quest);
        ScriptAttachment.OfScript("Haos_Init")
            .SetProperty("InitPerk", enablerPerk)
            .ApplyTo(quest);

        return quest;
    }

    private AQuestAlias CreatePlayerAlias()
    {
        var questAlias = new QuestReferenceAlias
        {
            UniqueActor = new FormLinkNullable<INpcGetter>(new FormKey(new ModKey("Starfield", ModType.Master), 7))
        };

        return questAlias;
    }



    private Perk AddModEnablerPerk(IEnumerable<ISpellGetter> abilitiesToActivate, IEnumerable<IPerkGetter> perksToActivate)
    {
        var perk = outputMod.Perks.AddNew("HaOS_Mod_Perk");
        perk.Name = "HaOS Mod Perk";
        perk.Description = "Enables the HaOS mod subsystems";
        perk.Categroy = PerkCategory.None;
        perk.Flags = Perk.Flag.PcPlayable;

        perk.BackgroundSkills.AddRange(perksToActivate.Select(perk => perk.ToLinkGetter()));

        var perkRank = new PerkRank();
        perkRank.Effects.AddRange(abilitiesToActivate.Select(ability => 
            new PerkAbilityEffect
            {
                Ability = ability.ToLink()
            }
        ));

        perk.Ranks.Add(perkRank);
        return perk;
    }

}