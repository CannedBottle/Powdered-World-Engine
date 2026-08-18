using Godot;
using System;
using System.IO;

[Tool]
[GlobalClass]
public partial class ElementEnumGenerator : RefCounted
{
    
    public static string elementScriptPath = "res://addons/powder_engine/PowderedWorldEngine/Scripts/CSharp/Elements.cs";

    public static string codeStart = 
"""
using Godot;
using System;

[Tool]
[GlobalClass]
public partial class Elements : Resource
{

    // WARNING: This file was automatically generated. Do not manually edit if you want to keep your soul.

    public static Godot.Collections.Array<StringName> GetElementNames()
    {
        Godot.Collections.Array<StringName> ReturnList = new Godot.Collections.Array<StringName>{};

        foreach(string name in AllElements.GetNames<AllElements>())
        {
            ReturnList.Add((StringName)name);
        }

        return ReturnList.Duplicate();
    }

    public static AllElements GetFromName(StringName name)
    {
        return (AllElements)Array.IndexOf(Enum.GetNames<AllElements>(), name);
    }

    public enum AllElements
    {

""";

public static string codeEnd = 
"""

    }
}
""";


    public static void GenerateElementAttributes()
    {
        string targetGlobalPath = ProjectSettings.GlobalizePath(elementScriptPath);


        if (!File.Exists(targetGlobalPath))
        {
            GD.PrintErr("read/write failed because file does not exist at " + elementScriptPath);
            return;
        }

        try
        {

            
            
            File.WriteAllText(targetGlobalPath, codeStart + GetEnumContents() + codeEnd);

        } catch(Exception ex)
        {
            GD.PrintErr("Failed to update Elements file: " + ex.Message);
            return;
        }


        

        EditorInterface.Singleton.GetResourceFilesystem().Scan();

    }

    
    public static string GetEnumContents()
    {
        string enumContents = "";

        foreach(string name in SandInfo.ElementResource.ElementOrder)
        {
            enumContents += "       " + name + """
            ,
            
            """;
        }

        return enumContents;
    }

}
