using Godot;
using System;
using static SandInfoCS;
using static Elements;
using static ElementAttributes;

[Tool]
[GlobalClass]
public partial class ElementStorage : Resource
{

	[Signal]
	public delegate void ElementsSavedEventHandler();

	public static readonly ElementAttributes DefaultElement = new ElementAttributes(AllElements.AIR, ElementTypes.STATIC, MoveTypes.NONE, new Color(1.0f, 0.0f, 0.0f, 1.0f), 0.1f, null);

	public static ElementAttributes GetDefaultElement()
	{
		return DefaultElement.Clone();
	}

	private static readonly Godot.Collections.Dictionary<StringName, ElementAttributes> DefaultAttributes = new Godot.Collections.Dictionary<StringName, ElementAttributes>
	{
		{(StringName)"AIR", new ElementAttributes((AllElements)0, ElementTypes.STATIC, MoveTypes.NONE, new Color(0.0f, 0.0f, 0.0f, 0.0f), 0.0f, null)},
		{(StringName)"SAND", new ElementAttributes((AllElements)1, ElementTypes.SOLID, MoveTypes.SAND, new Color(1.0f, 0.93f, 0.474f, 1.0f), 0.1f, null)},
		{(StringName)"WATER", new ElementAttributes((AllElements)2, ElementTypes.LIQUID, MoveTypes.LIQUID, new Color(0.112f, 0.644f, 0.93f, 0.6f), 0.0f, null)},
		{(StringName)"ACID", new ElementAttributes((AllElements)3, ElementTypes.LIQUID, MoveTypes.LIQUID, new Color(0.678f, 1.0f, 0.31f, 0.75f), 0.0f, null)},
		{(StringName)"STONE", new ElementAttributes((AllElements)4, ElementTypes.SOLID, MoveTypes.STONE, new Color(0.58f, 0.58f, 0.58f, 1.0f), 0.1f, null)},
		{(StringName)"WALL", new ElementAttributes((AllElements)5, ElementTypes.STATIC, MoveTypes.NONE, new Color(0.27f, 0.27f, 0.27f, 1.0f), 0.1f, null)},
	};


	[Export] public Godot.Collections.Dictionary<StringName, ElementAttributes> AllElementAttributes = new Godot.Collections.Dictionary<StringName, ElementAttributes>{};


	public static Godot.Collections.Dictionary<StringName, ElementAttributes> GetDefaultElementDict()
	{
		return DefaultAttributes;	
	}

	public void ResetDefaults()
	{
		Godot.Collections.Dictionary<StringName, ElementAttributes> ReturnDict = new Godot.Collections.Dictionary<StringName, ElementAttributes>{};

		foreach(StringName name in DefaultAttributes.Keys)
		{
			ReturnDict.Add(name, DefaultAttributes[name].Clone());

			ElementsSaved += ReturnDict[name].OnSave;
		}

		AllElementAttributes = ReturnDict.Duplicate(true);
	}

}
