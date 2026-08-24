using System.Collections.Generic;
using System.IO;
using System.Text;


/// <summary>
/// Globals are persisted from their GLOB record when a save is first created.
/// We need a script to overwrite their values to the rebalanced values of the mod
/// </summary>
public class StupidGlobWriter
{
    private readonly ChangedGlobCollection globChanges;

    public StupidGlobWriter(ChangedGlobCollection globChanges)
    {
        this.globChanges = globChanges;
    }

    public void Write(Stream file)
    {
        string scriptContent = CreateScriptContent();

        using StreamWriter writer = new(file);
        writer.Write(scriptContent);
    }

    public void Write(string fileName)
    {
        using FileStream fs = File.Open(fileName, FileMode.Create);
        Write(fs);
    }

    private string CreateScriptContent()
    {
        return new StupidGLOBFormatter(globChanges).FormatScript();
    }
}

public class StupidGLOBFormatter
{

    private ChangedGlobCollection list;

    public StupidGLOBFormatter(ChangedGlobCollection list)
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
        int currentIndex = 0;
        foreach (var item in list)
        {
            string lookupKey = GetLookupString(item.editorId);
            float value = item.globalValue;

            if (currentIndex == 0)
                ss.AppendLine($"If keyVal == \"{lookupKey}\"");
            else 
                ss.AppendLine($"ElseIf keyVal == \"{lookupKey}\"");
            
            ss.AppendLine($"\tReturn {value}");
            currentIndex++; 
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