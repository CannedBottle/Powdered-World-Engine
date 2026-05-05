using Godot;
using System;
using System.Collections.Generic;



[GlobalClass, Icon("uid://b0ol6juljiyfp")]
public partial class PowderSimulationCs : Node2D
{
	/// <summary>
	/// shows a visual of chunk borders and dirty rects.
	/// </summary>
	[Export] public bool DebugMode = false;
	/// <summary>
	/// The color of the chunk borders if DebugMode is turned on. 
	/// </summary>
	[Export] public Color DebugChunkBorderColor = new Color(1f, 0f, 0f, 0.9f);
	/// <summary>
	/// draws a border around the simulation.
	/// </summary>
	[Export] public bool ShowSimBorder = false;
	[Export] public int SimBorderWidth = 5;
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

	private int _individualChunkSize;
	/// <summary>
	/// How many pixels (on each side) each chunk contains.
	///
	/// IF CHANGING HIGHER THAN VALUE IN CHUNKRENDER SHADER, change the size of the array in the chunkrender shader as well.
	/// </summary>
	[Export(PropertyHint.Range, "1,9223372036854775807")] public int IndividualChunkSize
	{
		get => _individualChunkSize;
		set
		{
			if (IsInsideTree() != true)
			{
				_individualChunkSize = value;
			}
			else
			{
				GD.PushWarning("Attempted to change individual_chunk_size, when it should only be changed in the Inspector or before entering the SceneTree.");
			}
		}
	}


	private Vector2I _chunkGridSize;
	/// <summary>
	/// how many chunks to be used in the entire simulation.
	/// </summary>
	[Export] public Vector2I ChunkGridSize
	{
		get => _chunkGridSize; 
		set
		{
			_chunkGridSize = value;
			UpdateSimulationSize();
		}
	}


	/// <summary>
	/// How many update loops with no change have to occur before sleeping the chunk.
	/// </summary>
	[Export] public int ChunkInsomnia = 2;


	public Dictionary<Vector2I, SandInfoCS.Chunk> Chunks = new Dictionary<Vector2I, SandInfoCS.Chunk>();

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

	public List<ChunkRendererCS> ActiveChunkRenderers = new List<ChunkRendererCS>();
	public Node2D ChunkRendererParent;


	public void InitGrid()
	{
		Chunks.Clear();

		for(int y = 0; y < ChunkGridSize.Y; y++)
		{
			for(int x = 0; x < ChunkGridSize.X; x++)
			{
				Vector2I LoopPos = new Vector2I(x, y);

				Chunks.Add(LoopPos, new SandInfoCS.Chunk(IndividualChunkSize, LoopPos, SimulationSize, ChunkInsomnia));

				ChunkRendererCS Render = new ChunkRendererCS();
				Render.ChunkSize = IndividualChunkSize;
				Render.PixelScale = PixelScale;
				Render.Position = LoopPos * IndividualChunkSize * PixelScale;
				if (DebugMode)
				{
					Render.ShowDebugInfo = true;
				}

				ChunkRendererParent.AddChild(Render);
				ActiveChunkRenderers.Add(Render);

				Chunks[LoopPos].Renderer = Render;
			}
		}
	}

	public void UpdateChunks(int tick)
	{
		UpdateTime = Time.GetTicksUsec() / 1000.0f;
		// ----------------------------------------------------------------------------
		foreach(SandInfoCS.Chunk chunk in Chunks.Values)
		{
			chunk.UpdateCells(this, tick);
		}

		if (UseDirtyRects)
		{
			foreach(SandInfoCS.Chunk chunk in Chunks.Values)
			{
				chunk.UpdateDirtyRect();
			}
		}
		//-----------------------------------------------------------------------------
		UpdateTime = Time.GetTicksUsec() / 1000.0f - UpdateTime;
		if(DebugMode)
		{
			QueueRedraw();
		}
	}


	public void RenderChunkUpdates()
	{
		DrawTime = Time.GetTicksUsec() / 1000.0f;
		// -----------------------------------------------------------------
		foreach(SandInfoCS.Chunk chunk in Chunks.Values)
		{
			chunk.SendDrawInfoToRenderer();
		}
		// -----------------------------------------------------------------
		DrawTime = Time.GetTicksUsec() / 1000.0f - DrawTime;
	}


	public bool PlaceElement(Vector2I pos, SandInfoCS.Elements element, bool overRide = false)
	{
		if(pos.X < 0 || pos.Y < 0 || pos.X > SimulationSize.X || pos.Y > SimulationSize.Y)
		{
			return false;
		}


		// pos of chunk the cell is being placed in
		Vector2I ChunkPosition = new Vector2I(pos.X / IndividualChunkSize, pos.Y / IndividualChunkSize);
		SandInfoCS.Chunk chunk = Chunks[ChunkPosition];
		int LocalCellIdx = SandInfoCS.WorldPosToChunkPos(pos, IndividualChunkSize);
		chunk.Wake();

		SandInfoCS.Cell cell = chunk.Cells[LocalCellIdx];

		if(overRide == false && cell.Element != SandInfoCS.Elements.AIR)
		{
			return false;
		}
		else
		{
			cell.Element = element;
			chunk.MarkCellUpdated(cell);
			return true;
		}
	}


	public void PlaceGroupElements(int brushSize, Vector2I pos, SandInfoCS.Elements element, bool overRide = false)
	{
		for (int y = 0; y < brushSize * 2 + 1; y++)
		{
			for (int x = 0; x < brushSize * 2 + 1; x++)
			{
				PlaceElement(pos + new Vector2I(x - brushSize, y - brushSize), element, overRide);
			}
		}
	}


	/// <summary>
	/// Updates the simulation_size variable if chunk grid size has been changed. This is automatically done, so no need to call this.
	/// </summary>
	public void UpdateSimulationSize()
	{
		SimulationSize = new Vector2I(ChunkGridSize.X * IndividualChunkSize - 1, ChunkGridSize.Y * IndividualChunkSize - 1);
	}

	public override void _Ready()
	{
		InvChunkSize = 1.0f / IndividualChunkSize;


		ChunkRendererParent = new Node2D();
		AddChild(ChunkRendererParent);

		InitGrid();
	}

	// used to draw dirty rects for debug purposes.
    public override void _Draw()
    {
        base._Draw();

		if(ShowSimBorder)
		{
			DrawRect(new Rect2(new Vector2(-SimBorderWidth / 2, -SimBorderWidth / 2), SimulationSize * PixelScale + new Vector2(PixelScale + SimBorderWidth, PixelScale + SimBorderWidth)), new Color(0, 0, 0, 1.0f), false, SimBorderWidth, false);
		}


		if (!DebugMode)
		{
			return;
		}

		//dirty rects
		Vector2I CenteringVal = new Vector2I(PixelScale, PixelScale);
		foreach(SandInfoCS.Chunk chunk in Chunks.Values)
		{

			//dirty rects
			if (chunk.HasValidDirtyRect)
			{
				Vector2I Size = chunk.DirtyRectMax - chunk.DirtyRectMin;
				DrawRect(new Rect2(chunk.DirtyRectMin * PixelScale - new Vector2(0.5f, 0.5f), Size * PixelScale + CenteringVal), new Color(0.85f, 0.317f, 0.008f, 1.0f), false, 1, false);
			}

			// chunk borders
			Vector2 RectSize = new Vector2(chunk.ChunkSize * PixelScale, chunk.ChunkSize * PixelScale);
			Color col = DebugChunkBorderColor;
			float thickness = 3;
			if (chunk.Sleeping)
			{
				col.A = 0.25f;
			}

			DrawRect(new Rect2(chunk.ChunkPosition * chunk.ChunkSize * PixelScale + new Vector2(thickness / 2, thickness / 2), RectSize - new Vector2(thickness, thickness)), col, false, thickness, false);
		}

    }

	public void UpdateSimulation()
	{
		
		Accumulator += (float)GetProcessDeltaTime() * SimulationSpeed;

		if(Accumulator >= SimDt)
		{
			UpdateChunks(TicksPassed);
			RenderChunkUpdates();

			TicksPassed += 1;
			TicksPassed %= 2;
			Accumulator -= SimDt;
		}

	}


}
