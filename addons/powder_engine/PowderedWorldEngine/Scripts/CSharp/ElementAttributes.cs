using Godot;
using static SandInfo;
using static Elements;
using System.Collections.Generic;
using System.Linq;

[Tool]
[GlobalClass]
public partial class ElementAttributes : Resource
{
	// ---------------- STATIC -----------------------------
	public enum MoveTypes
	{
		SAND,
		LIQUID,
		STONE,
		GAS,
		NONE,
		CUSTOM,
	}

    
	public static Godot.Collections.Array<StringName> GetMoveTypes()
	{
		Godot.Collections.Array<StringName> MoveTypeArray = new Godot.Collections.Array<StringName>{};

		foreach(string name in MoveTypes.GetNames<MoveTypes>())
		{
			MoveTypeArray.Add((StringName)name);
		}

		return MoveTypeArray.Duplicate();

	}


	public static Dictionary<MoveTypes, Dictionary<string, byte>> MoveTypeDefaultFields = new Dictionary<MoveTypes, Dictionary<string, byte>>
	{
		{MoveTypes.NONE, new Dictionary<string, byte>{{"none", 0}}},
		
		{MoveTypes.SAND, new Dictionary<string, byte>{{"R#random", 1}}},
		
		{MoveTypes.STONE, new Dictionary<string, byte>{{"none", 0}}},
		
		{MoveTypes.LIQUID, new Dictionary<string, byte> 
		{
			{"R#random", 1},
			{"R#direction", 0},
			{"bumps", 0},
		}},
		
		{MoveTypes.GAS, new Dictionary<string, byte>{{"R#random", 7}}},
	};


	public static Reaction CreateReaction(Godot.Collections.Array<Vector2I> MarkedNeighbors, AllElements ElementToCheck, int NeighborComparison, int ReactionType, AllElements ReplaceElement, int MatchingNumToCheck, Godot.Collections.Array<AllElements> elementExclusions)
	{
		return new Reaction(MarkedNeighbors, ElementToCheck, (Reaction.Comparisons)NeighborComparison, (Reaction.ReactionTypes)ReactionType, ReplaceElement, MatchingNumToCheck, elementExclusions);
	}

	

	public static Reaction GetDefaultReaction()
	{
		return new Reaction(new Godot.Collections.Array<Vector2I>{}, null, Reaction.Comparisons.GREATER_THAN_OR_EQUAL, Reaction.ReactionTypes.REPLACE_SELF, AllElements.AIR, 1);
	}

	// ----------------------------------------------------------------------------------


	/// <summary>
	/// 
	/// </summary>
	/// <returns>the default array of the element-specific fields needed for its movement.</returns>
	public byte[] GetDefaultFieldArray()
	{
		byte[] arr = new byte[FieldToIndex.Count];

		arr = MoveTypeDefaultFields[MovementType].Values.ToArray();

		int i = 0;
		foreach(string field in FieldToIndex.Keys)
		{
			if (field.StartsWith("R#"))
			{
				arr[FieldToIndex[field]] = (byte)GD.RandRange(0, arr[FieldToIndex[field]]);
			}

			i++;
		}

		return arr;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="field"></param>
	/// <returns>The index of the specified field for the current element.</returns>
	public int FieldGetIndex(string field)
	{
		return FieldToIndex[field];
	}

    [Export] public AllElements Id {get; set;}

	[Export] public ElementTypes Type {get; set;}

	[Export] public string StringName {get; set;}

	private MoveTypes _movetype;
	[Export] public MoveTypes MovementType
	{
		get => _movetype;

		set
		{
			_movetype = value;

			_GenerateFields();
		}
	}

	#nullable enable
	[Export] public Godot.Collections.Array<ElementFlags>? Flags {get; set;}

	[Export] public Color BaseColor {get; set;} = new Color(0.0f, 0.0f, 0.0f, 1.0f);

	[Export] public float NoiseStrength {get; set;}

	[Export] public Godot.Collections.Array<Reaction> Reactions {get; set;} = new Godot.Collections.Array<Reaction>{};

	// -------- Custom Fields ----------------------

	[Export] public Godot.Collections.Dictionary<string, byte> FieldToIndex = new Godot.Collections.Dictionary<string, byte>{};

	public struct FieldProperties
	{
		public int Index;
		public int DefaultValue;

		public FieldProperties(int defaultValue, int index)
		{
			Index = index;
			DefaultValue = defaultValue;
		}
	}

	// ---------------------------------------------

	public ElementAttributes(AllElements EId, ElementTypes EType, MoveTypes EMoveType, Color EColor, float ENoiseStrength, Godot.Collections.Array<ElementFlags>? EFlags, Godot.Collections.Array<Reaction> EReactions)
	{
			
		Id = EId;
		Type = EType;
		MovementType = EMoveType;
		BaseColor = EColor;
		NoiseStrength = ENoiseStrength;
		Flags = EFlags;
		Reactions = EReactions;
		
		if(Flags == null)
		{
			Flags = new Godot.Collections.Array<ElementFlags>{};
		}
		if(Reactions == null)
		{
			Reactions = new Godot.Collections.Array<Reaction>{};
		}

		_GenerateFields();

	}
	#nullable disable

	public ElementAttributes()
	{
		
	}

	private void _GenerateFields()
	{
		if(MovementType != MoveTypes.CUSTOM)
		{ 
			FieldToIndex = new Godot.Collections.Dictionary<string, byte>(MoveTypeDefaultFields[MovementType]);
			
			int i = 0;
			foreach(string field in FieldToIndex.Keys)
			{
				FieldToIndex[field] = (byte)i;

				i++;
			}
		}
	}

	public void OnSave()
	{
		_GenerateFields();
	}

	public ElementAttributes Clone()
	{
		Godot.Collections.Array<Reaction> clonedReactions = new Godot.Collections.Array<Reaction>{};
		foreach(Reaction reaction in Reactions)
		{
			clonedReactions.Add((Reaction)reaction.Duplicate(true));
		}

		return new ElementAttributes(Id, Type, MovementType, BaseColor, NoiseStrength, Flags.Duplicate(true), clonedReactions);
	}

	public StringName GetTypeStrN()
	{
		return (StringName)Type.ToString();
	}

	public StringName GetMoveTypeStrN()
	{
		return (StringName)MovementType.ToString();
	}

	public void AddFlag(int flagIndex)
	{
		Flags.Add((ElementFlags)flagIndex);
	}

	public void RemoveFlag(int index)
	{
		Flags.RemoveAt(index);
	}

	public int GetFlagsCount()
	{
		return Flags.Count;
	}

	public void AddReaction(Reaction reaction)
	{
		Reactions.Add(reaction);
	}

	public void RemoveReaction(int Idx)
	{
		Reactions.RemoveAt(Idx);
	}

	// GET REACTION DATA FUNCTIONS -------------------------------------------------------------------

	public Godot.Collections.Array<Vector2I> GetReactionMarkedNeighbors(int ReactionIdx)
	{
		return Reactions[ReactionIdx].MarkedNeighbors.Duplicate();
	}

	public Godot.Collections.Array<AllElements> GetReactionElementExclusions(int ReactionIdx)
	{
		return Reactions[ReactionIdx].ElementExclusions.Duplicate();
	}

	/// <summary></summary>
	/// <param name="ReactionIdx"></param>
	/// <returns>-1 if it means ANY element, but otherwise returns the specific element enum index.</returns>
	public int GetReactionElementCheck(int ReactionIdx)
	{
		return Reactions[ReactionIdx].ElementCheck != null ? (int)Reactions[ReactionIdx].ElementCheck : -1;
	}

	public int GetReactionNeighborElementComparison(int ReactionIdx)
	{
		return (int)Reactions[ReactionIdx].NeighborElementComparison;
	}

	public int GetReactionType(int ReactionIdx)
	{
		return (int)Reactions[ReactionIdx].reactionType;
	}

	public int GetReactionReplaceElement(int ReactionIdx)
	{
		return (int)Reactions[ReactionIdx].ReplaceWith;
	}

	public int GetReactionMatchingNumCheck(int ReactionIdx)
	{
		return Reactions[ReactionIdx].MatchingNumToCheck;
	}

	// SET REACTION DATA FUNCTIONS -------------------------------------------------------------------

	public void SetReactionMarkedNeighbors(int ReactionIdx, Godot.Collections.Array<Vector2I> NewMarkedNeighbors)
	{
		Reactions[ReactionIdx].MarkedNeighbors = NewMarkedNeighbors;
	}

	public void SetReactionElementExclusions(int ReactionIdx, Godot.Collections.Array<int> NewElementExclusions)
	{
		Godot.Collections.Array<AllElements> newArray = new Godot.Collections.Array<AllElements>{};
		foreach(int elementIdx in NewElementExclusions)
		{
			newArray.Add((AllElements)elementIdx);
		}
		Reactions[ReactionIdx].ElementExclusions = newArray;
	}


	public void SetReactionElementCheck(int ReactionIdx, int NewElementCheck)
	{
		Reactions[ReactionIdx].ElementCheck = NewElementCheck == -1 ? null : (AllElements)NewElementCheck;
	}

	public void SetReactionNeighborElementComparison(int ReactionIdx, int NewNeighborElementComparison)
	{
		Reactions[ReactionIdx].NeighborElementComparison = (Reaction.Comparisons)NewNeighborElementComparison;
	}

	public void SetReactionType(int ReactionIdx, int NewReactionType)
	{
		Reactions[ReactionIdx].reactionType = (Reaction.ReactionTypes)NewReactionType;
	}

	public void SetReactionReplaceElement(int ReactionIdx, int NewReactionReplaceElement)
	{
		Reactions[ReactionIdx].ReplaceWith = (AllElements)NewReactionReplaceElement;
	}

	public void SetReactionMatchingNumCheck(int ReactionIdx, int NewMatchingNumCheck)
	{
		Reactions[ReactionIdx].MatchingNumToCheck = NewMatchingNumCheck;
	}

	// ---------------------------------------------------------------------------

}
