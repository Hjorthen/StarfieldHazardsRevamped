using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;

namespace RecordCollections.Basegame;

public class ExtremeEnvironmentsPlanetaryConditions
{
    private static readonly List<(string type, string editorId)> planetaryConditions = [
        ("Radiation", "PEO_ENV_CND_ExtremeEnvironment_Radiation_Solar"),
        ("Thermal", "PEO_ENV_CND_ExtremeEnvironment_Heat"),
        ("Corrosive", "PEO_ENV_CND_ExtremeEnvironment_Corrosive"),
        ("Airborne", "PEO_ENV_CND_ExtremeEnvironment_Toxic")
    ];

    private readonly Dictionary<string, IFormLinkGetter<IConditionRecord>> conditionRecords = [];

    private ExtremeEnvironmentsPlanetaryConditions(Dictionary<string, IFormLinkGetter<IConditionRecord>> conditionRecords)
    {
        this.conditionRecords = conditionRecords;
    }

    public IFormLinkGetter<IConditionRecord> Resolve(string hazard) => conditionRecords[hazard];

    public static ExtremeEnvironmentsPlanetaryConditions CreateInstance(ILinkCache linkCache)
    {
        return new ExtremeEnvironmentsPlanetaryConditions(
            conditionRecords: planetaryConditions.ToDictionary(
                cond => cond.type, 
                cond => {
                    var formKey = linkCache.ResolveIdentifier<IConditionRecordGetter>(cond.editorId);
                    return new FormLinkGetter<IConditionRecord>(formKey) as IFormLinkGetter<IConditionRecord>;
                }
            )
        );
    }
}