using Godot;
using System;
using static SandInfoCS;
using static Elements;

[GlobalClass]
public partial class ElementAttributes : Resource
{
    
    [Export] public AllElements Id {get; set;}

	[Export] public ElementTypes Type {get; set;}

	[Export] public string StringName {get; set;}

	#nullable enable
	[Export] public Godot.Collections.Array<ElementFlags>? Flags {get; set;}

	[Export] public Color BaseColor {get; set;} = new Color(0.0f, 0.0f, 0.0f, 1.0f);

	[Export] public float NoiseStrength {get; set;}

	public ElementAttributes(AllElements EId, ElementTypes EType, Color EColor, float ENoiseStrength, Godot.Collections.Array<ElementFlags>? EFlags)
	{
			
		Id = EId;
		Type = EType;
		BaseColor = EColor;
		NoiseStrength = ENoiseStrength;
		Flags = EFlags;	

	}
	#nullable disable

	public ElementAttributes()
	{
		
	}

	public ElementAttributes Clone()
	{
		return new ElementAttributes(Id, Type, BaseColor, NoiseStrength, Flags);
	}

}
