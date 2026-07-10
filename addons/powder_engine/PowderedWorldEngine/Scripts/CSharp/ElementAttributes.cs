using Godot;
using System;
using static SandInfoCS;
using static Elements;

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



	public struct Reaction
	{
		/// <summary>
		/// The neighbors the reaction checks to find if the it meets the criteria to cause the reaction.
		/// </summary>
		public Godot.Collections.Array<string> CheckedNeighbors;

	}

	// ----------------------------------------------------------------------------------

    [Export] public AllElements Id {get; set;}

	[Export] public ElementTypes Type {get; set;}

	[Export] public string StringName {get; set;}

	[Export] public MoveTypes MovementType {get; set;}

	#nullable enable
	[Export] public Godot.Collections.Array<ElementFlags>? Flags {get; set;}

	[Export] public Color BaseColor {get; set;} = new Color(0.0f, 0.0f, 0.0f, 1.0f);

	[Export] public float NoiseStrength {get; set;}

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

	}
	#nullable disable

	public ElementAttributes()
	{
		
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
