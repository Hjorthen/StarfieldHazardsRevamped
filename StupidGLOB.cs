using System.Collections.Generic;
using System.IO;
using System.Text;


/// <summary>
/// Globals are persisted from their GLOB record when a save is first created.
/// We need a script to overwrite their values to the rebalanced values of the mod
/// </summary>
public class StupidGlobWriter
{
    public void Write(Stream file)
    {
        List<StupidGLOBFormatter.GlobEntry> globalGetters = GetGlobalsToConfigure();

        string scriptContent = CreateScriptContent(globalGetters);

        using StreamWriter writer = new(file);
        writer.Write(scriptContent);
    }

    public void Write(string fileName)
    {
        using FileStream fs = File.Open(fileName, FileMode.Create);
        Write(fs);
    }

    private static string CreateScriptContent(List<StupidGLOBFormatter.GlobEntry> globalGetters)
    {
        return new StupidGLOBFormatter(globalGetters).FormatScript();
    }

    private static List<StupidGLOBFormatter.GlobEntry> GetGlobalsToConfigure()
    {
        var settings = new EnvDamageSettings();
        List<StupidGLOBFormatter.GlobEntry> globalGetters = [];
        foreach (var item in settings.GetGlobNames())
        {
            float value = settings.GetValue(item);
            globalGetters.Add(new StupidGLOBFormatter.GlobEntry(item, value));
        }

        return globalGetters;
    }

}

public class StupidGLOBFormatter
{
    public record GlobEntry (string editorId, float globalValue);

    private IList<GlobEntry> list;

    public StupidGLOBFormatter(IList<GlobEntry> list)
    {
        this.list = list;
    }

    private void WriteGlobalsLookup(StringBuilder ss)
    {
        ss.AppendLine("ScriptName Haos_GlobalOverrides");
        ss.AppendLine("");
        ss.AppendLine("");
        AppendLookupFunction(ss);
    }

    private void AppendLookupFunction(StringBuilder ss)
    {
        ss.AppendLine("Float Function Lookup(String keyVal) Global");
        AppendGlobals(ss);
        ss.AppendLine("EndFunction");
    }

    private void AppendGlobals(StringBuilder ss)
    {
        for (int i = 0; i < list.Count; i++)
        {
            string lookupKey = GetLookupString(list[i].editorId);
            float value = list[i].globalValue;

            if (i == 0)
                ss.AppendLine($"If keyVal == \"{lookupKey}\"");
            else 
                ss.AppendLine($"ElseIf keyVal == \"{lookupKey}\"");
            
            ss.AppendLine($"\tReturn {value}");
            
        }

        ss.AppendLine("EndIf");
    }

    public string FormatScript()
    {
        StringBuilder ss = new StringBuilder();
        WriteGlobalsLookup(ss);
        return ss.ToString();
    }

    public static string GetLookupString(string editorId)
    {
        return editorId.Replace("ENV_", "").Replace("PEO_", "");
    }
}