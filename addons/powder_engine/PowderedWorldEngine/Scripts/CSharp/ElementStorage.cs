using Godot;
using System;
using static SandInfoCS;
using static Elements;

[Tool]
[GlobalClass]
public partial class ElementStorage : Resource
{

	/* Defaults Order:

		AIR
		SAND
		WATER
		ACID
		STONE
		WALL
	*/
	public Godot.Collections.Dictionary<StringName, ElementAttributes> DefaultAttributes = new Godot.Collections.Dictionary<StringName, ElementAttributes>
	{
		{(StringName)"AIR", new ElementAttributes((AllElements)0, ElementTypes.STATIC, new Color(0.0f, 0.0f, 0.0f, 0.0f), 1.0f, null)},
		{(StringName)"SAND", new ElementAttributes((AllElements)1, ElementTypes.SOLID, new Color(1.0f, 0.93f, 0.474f, 1.0f), 1.0f, null)},
		{(StringName)"WATER", new ElementAttributes((AllElements)2, ElementTypes.LIQUID, new Color(0.112f, 0.644f, 0.93f, 0.6f), 0.0f, null)},
		{(StringName)"ACID", new ElementAttributes((AllElements)3, ElementTypes.LIQUID, new Color(0.678f, 1.0f, 0.31f, 0.75f), 0.0f, null)},
		{(StringName)"STONE", new ElementAttributes((AllElements)4, ElementTypes.SOLID, new Color(0.58f, 0.58f, 0.58f, 1.0f), 0.0f, null)},
		{(StringName)"WALL", new ElementAttributes((AllElements)5, ElementTypes.STATIC, new Color(0.27f, 0.27f, 0.27f, 1.0f), 0.0f, null)},
	};


	[Export] public Godot.Collections.Dictionary<StringName, ElementAttributes> AllElementAttributes = new Godot.Collections.Dictionary<StringName, ElementAttributes>{};


    public Godot.Collections.Array<String> ElementNames = new Godot.Collections.Array<String>
	{
		"AIR",
		"SAND",
		"WATER",
		"ACID",
		"STONE",
		"WALL",
	};


	public void ResetDefaults()
	{
		AllElementAttributes = DefaultAttributes.Duplicate();
	}

}
