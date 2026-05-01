using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;


[GlobalClass, Icon("uid://b0ol6juljiyfp")]
public partial class PowderSimulationCs : Node2D
{
	/// <summary>
	/// shows a visual of chunk borders and dirty rects.
	/// </summary>
	[Export] public bool DebugMode = false;
	/// <summary>
	/// draws a border around the simulation.
	/// </summary>
	[Export] public bool ShowSimBorder = false;
	/// <summary>
	/// How big the pixels appear on the screen.
	/// </summary>
	[Export] public int PixelScale = 5;
	/// <summary>
	/// How fast the simulation runs, with 1.0 corresponding with 60 updates/second.
	/// </summary>
	[Export] public float SimulationSpeed = 1.0f;
	/// <summary>
	/// Dirty rects are rectangles calculated at runtime that only contain the cells that were updated in the last frame, lowering the total number of cells needing updates.
	/// 
	/// Recommended for chunk sizes 32 and above.
	/// </summary>
	[Export] public bool UseDirtyRects = true;
	/// <summary>
	/// How many pixels (on each side) each chunk contains.
	///
	/// IF CHANGING HIGHER THAN VALUE IN CHUNKRENDER SHADER, change the size of the array in the chunkrender shader as well.
	/// </summary>
	[Export(PropertyHint.Range, "1,9223372036854775807")] public int IndividualChunkSize
	{
		get => IndividualChunkSize;
		set
		{
			if (IsInsideTree() != true)
			{
				IndividualChunkSize = value;
			}
			else
			{
				GD.PushWarning("Attempted to change individual_chunk_size, when it should only be changed in the Inspector or before entering the SceneTree.");
			}
		}
	}

	/// <summary>
	/// how many chunks to be used in the entire simulation.
	/// </summary>
	[Export] public Vector2I ChunkGridSize
	{
		get => ChunkGridSize; 
		set
		{
			ChunkGridSize = value;
			UpdateSimulationSize();
		}
	}


	/// <summary>
	/// How many update loops with no change have to occur before sleeping the chunk.
	/// </summary>
	[Export] public int ChunkInsomnia = 2;


	public Dictionary<Vector2I, SandInfoCS.Chunk> Chunks;

	public Vector2I SimulationSize;

	// the amount of time it takes to update and draw, respectively. used for debug purposes.
	public float UpdateTime = 0.0f;
	public float DrawTime = 0.0f;
	
	// update 60 times/sec
	private float SimDt = 1.0f / 60.0f;
	// time passed since startup
	private float Accumulator = 0.0f;
	// number of ticks passed since startup, mod by 2
	private int TicksPassed = 1;

	public float InvChunkSize;

	public List<ChunkRendererCS> ActiveChunkRenderers;
	public Node2D ChunkRendererParent;

	

	/// <summary>
	/// Updates the simulation_size variable if chunk grid size has been changed. This is automatically done, so no need to call this.
	/// </summary>
	public void UpdateSimulationSize()
	{
		SimulationSize = new Vector2I(ChunkGridSize.X * IndividualChunkSize - 1, ChunkGridSize.Y * IndividualChunkSize - 1);
	}

	public override void _Ready()
	{
		
	}

    public override void _Draw()
    {
        base._Draw();
    }


}
