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

        foreach(string name in SandInfoCS.ElementNames)
        {
            enumContents += "       " + name + """
            ,
            
            """;
        }

        return enumContents;
    }

}
