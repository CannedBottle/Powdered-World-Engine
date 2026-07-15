using Godot;
using System;
using static SandInfoCS;
using static Elements;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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


	public static Dictionary<MoveTypes, Dictionary<string, int>> MoveTypeDefaultFields = new Dictionary<MoveTypes, Dictionary<string, int>>
	{
		{MoveTypes.NONE, new Dictionary<string, int>{{"none", 0}}},
		
		{MoveTypes.SAND, new Dictionary<string, int>{{"R#random", 1}}},
		
		{MoveTypes.STONE, new Dictionary<string, int>{{"none", 0}}},
		
		{MoveTypes.LIQUID, new Dictionary<string, int> 
		{
			{"R#random", 1},
			{"R#direction", 0},
			{"bumps", 0},
		}},
		
		{MoveTypes.GAS, new Dictionary<string, int>{{"R#random", 7}}},
	};


	public struct Reaction
	{
		/// <summary>
		/// The neighbors the reaction checks to find if the it meets the criteria to cause the reaction.
		/// </summary>
		public Godot.Collections.Array<Neighbors> CheckedNeighbors;

	}

	// ----------------------------------------------------------------------------------

	/// <summary>
	/// 
	/// </summary>
	/// <returns>the default array of the element-specific fields needed for its movement.</returns>
	public int[] GetDefaultFieldArray()
	{
		int[] arr = new int[FieldToIndex.Count];

		arr = MoveTypeDefaultFields[MovementType].Values.ToArray();

		int i = 0;
		foreach(string field in FieldToIndex.Keys)
		{
			if (field.StartsWith("R#"))
			{
				arr[FieldToIndex[field]] = GD.RandRange(0, arr[FieldToIndex[field]]);
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

	// -------- Custom Fields ----------------------

	[Export] public Godot.Collections.Dictionary<string, int> FieldToIndex = new Godot.Collections.Dictionary<string, int>{};

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

	public ElementAttributes(AllElements EId, ElementTypes EType, MoveTypes EMoveType, Color EColor, float ENoiseStrength, Godot.Collections.Array<ElementFlags>? EFlags)
	{
			
		Id = EId;
		Type = EType;
		MovementType = EMoveType;
		BaseColor = EColor;
		NoiseStrength = ENoiseStrength;
		Flags = EFlags;
		
		if(Flags == null)
		{
			Flags = new Godot.Collections.Array<ElementFlags>{};
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
			FieldToIndex = new Godot.Collections.Dictionary<string, int>(MoveTypeDefaultFields[MovementType]);
			
			int i = 0;
			foreach(string field in FieldToIndex.Keys)
			{
				FieldToIndex[field] = i;

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
		return new ElementAttributes(Id, Type, MovementType, BaseColor, NoiseStrength, Flags.Duplicate(true));
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

}
