using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;

namespace Records.Fluent;
/*
    Papyrus type	Mutagen class	Payload
    Object reference	ScriptObjectProperty	FormLink<SkyrimMajorRecord> Object, Alias (int16)
    String	ScriptStringProperty	string Data
    Int	ScriptIntProperty	int Data
    Float	ScriptFloatProperty	float Data
    Bool	ScriptBoolProperty	bool Data
    Object[]	ScriptObjectListProperty	list of ScriptObjectProperty
    String[]	ScriptStringListProperty	list of string
    Int[] / Float[] / Bool[]	ScriptIntListProperty / etc.	lists of primitives
*/
public class ScriptAttachment
{
    private readonly string _ScriptName;
    private List<ScriptProperty> _Properties;

    private ScriptAttachment(string scriptName)
    {
        _ScriptName = scriptName;
        _Properties = [];
    }

    public static ScriptAttachment OfScript(string scriptName)
    {
        return new ScriptAttachment(scriptName);
    }

    public ScriptAttachment SetProperty(string propertyName, FormLink<IActorValueInformationGetter> data)
    {
        _Properties.Add(new ScriptObjectProperty()
        {
            Name = propertyName,
            Object = data
        });

        return this;
    }

    public ScriptAttachment SetProperty(string propertyName, IEnumerable<IFormLink<IStarfieldMajorRecordGetter>> entries)
    {
        var entriesAsScriptProperty = entries.Select(e => new ScriptObjectProperty()
        {
            Object = e
        });

        var property = new ScriptObjectListProperty()
        {
            Name = propertyName,
            Objects = new Noggog.ExtendedList<ScriptObjectProperty>(entriesAsScriptProperty),
        };

        _Properties.Add(property);
        return this;
    }

    public ScriptAttachment SetProperty(string propertyName, float data)
    {
        _Properties.Add(new ScriptFloatProperty()
        {
            Name = propertyName,
            Data = data
        });
        
        return this;
    }
    public ScriptAttachment SetProperty(string name, string data)
    {
        _Properties.Add(new ScriptStringProperty()
        {
            Flags = ScriptProperty.Flag.Edited,
            Name = name,
            Data = data
        });
        return this;
    }

    public void ApplyTo(MagicEffect magicEffect) {
        magicEffect.VirtualMachineAdapter = new VirtualMachineAdapter()
        {
            // 6, 2 seems to be the values used from checking XEdit
            Version = 6,
            ObjectFormat = 2,
            Scripts = new Noggog.ExtendedList<ScriptEntry>()
            {
                new ScriptEntry()
                {
                    Flags = ScriptEntry.Flag.Local,
                    Name = _ScriptName,
                    Properties = new Noggog.ExtendedList<ScriptProperty>(_Properties)
                }                
            }

        };
        // Make sure the MF is set to script, in case it wasn't already
        magicEffect.Archetype = new MagicEffectArchetype()
        {
            Type = MagicEffectArchetype.TypeEnum.Script
        };
    }

}