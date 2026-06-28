using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Order;
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

var resolver = new BaseGameTypeResolver(linkCache);
var mapper = new HazardsMapper(
    hazardTypes: ["Thermal", "Airborne", "Corrosive", "Radiation"]
);

var hazardMod = new StarfieldMod("MyMod.esp", StarfieldRelease.Starfield);
var hazardSystem = HazardsSystemPatcher.WritePatch(hazardMod, mapper.HazardTypes, resolver);

var loadOrderWithHazards = priorityOrder.Concat(new List<IModListingGetter<IStarfieldModGetter>> {new ModListing<IStarfieldModGetter>(hazardMod.ModKey, hazardMod, enabled: true)});
linkCache = loadOrderWithHazards.ToImmutableLinkCache();
hazardSystem.SetLinkCache(linkCache);

HazardSystemArmorUpgrades.WritePatch(hazardSystem, hazardMod, linkCache);
HazardSystemItemsPatcher.WritePatch(hazardSystem, hazardMod, linkCache);
HazardsSystemSpellsPatcher.WritePatch(hazardMod, hazardSystem, mapper, resolver, env);
HazardSystemScalingResistancesPatcher.WritePatch(hazardSystem, hazardMod, linkCache);


hazardMod.BeginWrite
.ToPath(Path.Combine("", hazardMod.ModKey.FileName))
.WithLoadOrder(env.LoadOrder)
.Write();