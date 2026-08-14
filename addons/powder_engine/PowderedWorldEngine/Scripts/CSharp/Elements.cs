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
       AIR,
       SAND,
       WATER,
       ACID,
       STONE,
       WALL,

    }
}