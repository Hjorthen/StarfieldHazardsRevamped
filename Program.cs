using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;
using Noggog;
using OneOf.Types;

using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .WithTargetDataFolder("/home/sehj/.local/share/Steam/steamapps/common/Starfield/Data")
                .WithLoadOrder("Starfield.esm")
                .Build();

var priorityOrder = env.LoadOrder.PriorityOrder;
// The linkCache has to be created from the priority-order it seems

var linkCache = priorityOrder.ToImmutableLinkCache();

string[] types = ["Thermal", "Airborne", "Corrosive", "Radiation"];
var resolver = new BaseGameTypeResolver(linkCache);


new HazardsModBuilder(types, resolver)
    .AddSoakValues()
    .AddSoakDamageConditionForms()
    .AddExtremeEnvironmentMagicEffects()
    .PatchMagicEffects(priorityOrder.MagicEffect().WinningOverrides())
    .PatchSpellHazards(priorityOrder.Spell().WinningOverrides())
    .PatchRestoreSoak()
    //.DebugPrint();
    .WriteTo(priorityOrder.ToLoadOrder(), "");