using Godot;
using System;
using System.Linq;

[Tool]
[GlobalClass]
public partial class Elements : Resource
{
    
    public static Godot.Collections.Array<StringName> ElementNames = new Godot.Collections.Array<StringName>
    {
        (StringName)"SAND",
    };

    // ------------ GDScript Helper Functions -------------- //
    public static void AddElementName(StringName name)
    {
        ElementNames.Append(name);
    }

    public static void RemoveElementName(StringName name)
    {
        ElementNames.Remove(name);
    }

    public static Godot.Collections.Array<StringName> GetElementNames()
    {
        return ElementNames;
    }

    // ----------------------------------------------------- //

}
