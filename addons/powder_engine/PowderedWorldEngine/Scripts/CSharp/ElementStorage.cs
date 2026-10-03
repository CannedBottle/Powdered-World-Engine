using Godot;
using static SandInfo;
using static Elements;
using static ElementAttributes;

[Tool]
[GlobalClass]
public partial class ElementStorage : Resource
{

	[Signal]
	public delegate void ElementsSavedEventHandler();

	public static readonly Reaction AcidReaction = new Reaction(new Godot.Collections.Array<Vector2I>{TOPLEFT, TOPMIDDLE, TOPRIGHT, LEFTMIDDLE, RIGHTMIDDLE, BOTTOMLEFT, BOTTOMMIDDLE, BOTTOMRIGHT}, null, Reaction.Comparisons.GREATER_THAN_OR_EQUAL,
	Reaction.ReactionTypes.REPLACE_LAST_ITERATED_NEIGHBOR, AllElements.AIR, 1, new Godot.Collections.Array<AllElements>{AllElements.ACID});

	public static readonly Reaction AcidReaction2 = new Reaction(new Godot.Collections.Array<Vector2I>{TOPLEFT, TOPMIDDLE, TOPRIGHT, LEFTMIDDLE, RIGHTMIDDLE, BOTTOMLEFT, BOTTOMMIDDLE, BOTTOMRIGHT}, null, Reaction.Comparisons.GREATER_THAN_OR_EQUAL,
	Reaction.ReactionTypes.REPLACE_SELF, AllElements.AIR, 1, new Godot.Collections.Array<AllElements>{AllElements.ACID});

	public static readonly ElementAttributes DefaultElement = new ElementAttributes(ElementTypes.STATIC, MoveTypes.NONE, new Color(1.0f, 0.0f, 0.0f, 1.0f), 0.1f, null, null, null);

	public static ElementAttributes GetDefaultElement()
	{
		return DefaultElement.Clone();
	}

	private static readonly Godot.Collections.Dictionary<StringName, ElementAttributes> DefaultAttributes = new Godot.Collections.Dictionary<StringName, ElementAttributes>
	{
		{(StringName)"AIR", new ElementAttributes(ElementTypes.STATIC, MoveTypes.NONE, new Color(0.0f, 0.0f, 0.0f, 0.0f), 0.0f, null, null, null)},
		{(StringName)"SAND", new ElementAttributes(ElementTypes.SOLID, MoveTypes.SAND, new Color(1.0f, 0.93f, 0.474f, 1.0f), 0.1f, null, null, null)},
		{(StringName)"WATER", new ElementAttributes(ElementTypes.LIQUID, MoveTypes.LIQUID, new Color(0.112f, 0.644f, 0.93f, 0.6f), 0.0f, null, null, null)},
		{(StringName)"ACID", new ElementAttributes(ElementTypes.LIQUID, MoveTypes.LIQUID, new Color(0.678f, 1.0f, 0.31f, 0.75f), 0.0f, null, new Godot.Collections.Array<Reaction>{AcidReaction2.Clone(), AcidReaction.Clone()}, null)},
		{(StringName)"STONE", new ElementAttributes(ElementTypes.SOLID, MoveTypes.STONE, new Color(0.58f, 0.58f, 0.58f, 1.0f), 0.1f, null, null, null)},
		{(StringName)"WALL", new ElementAttributes(ElementTypes.STATIC, MoveTypes.NONE, new Color(0.27f, 0.27f, 0.27f, 1.0f), 0.1f, null, null, null)},
		{(StringName)"SMOKE", new ElementAttributes(ElementTypes.GAS, MoveTypes.GAS, new Color(0.102f, 0.102f, 0.102f, 0.49f), 0.025f, null, null, null)},
		{(StringName)"FIRE", new ElementAttributes(ElementTypes.GAS, MoveTypes.CUSTOM, new Color(1.0f, 0.584f, 0.0f), 0.15f, null, null, new FireBehavior())},
	};


	[Export] public Godot.Collections.Dictionary<StringName, ElementAttributes> AllElementAttributes = new Godot.Collections.Dictionary<StringName, ElementAttributes>{};

	// a list of all of the names of the elements. Required because Dictionaried get sorted alphabetically by Godot on engine reload.
	[Export] public Godot.Collections.Array<StringName> ElementOrder = new Godot.Collections.Array<StringName>{};

	public static Godot.Collections.Dictionary<StringName, ElementAttributes> GetDefaultElementDict()
	{
		return DefaultAttributes;	
	}

	public void ResetDefaults()
	{

		Godot.Collections.Dictionary<StringName, ElementAttributes> ReturnDict = new Godot.Collections.Dictionary<StringName, ElementAttributes>{};

		Godot.Collections.Array<StringName> OrderArray = new Godot.Collections.Array<StringName>{};

		foreach(StringName name in DefaultAttributes.Keys)
		{
			ReturnDict.Add(name, DefaultAttributes[name].Clone());
			OrderArray.Add(name);

			ElementsSaved += ReturnDict[name].OnSave;

		}

		AllElementAttributes = ReturnDict.Duplicate(true);
		ElementOrder = OrderArray.Duplicate(true);
	}

	public void UpdateIds()
	{
		foreach(StringName name in AllElementAttributes.Keys)
		{
			AllElementAttributes[name].StringName = name;
		}
	}

	public void ConnectSavedSignals()
	{
		foreach(StringName name in AllElementAttributes.Keys)
		{
			ElementsSaved -= AllElementAttributes[name].OnSave;
			ElementsSaved += AllElementAttributes[name].OnSave;
		}
	}

}
