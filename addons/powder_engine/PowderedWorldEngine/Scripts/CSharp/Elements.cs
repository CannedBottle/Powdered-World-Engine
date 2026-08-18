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
        if(!Enum.TryParse<AllElements>(name, out AllElements result))
        {
            GD.Print("failed to get " + name);
        }

        return result;
    }

    public enum AllElements
    {
       AIR,
       SAND,
       WATER,
       ACID,
       STONE,
       WALL,

    }
}