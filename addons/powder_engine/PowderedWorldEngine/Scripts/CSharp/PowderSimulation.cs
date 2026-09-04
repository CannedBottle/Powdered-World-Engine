using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static Elements;


[GlobalClass, Icon("uid://b0ol6juljiyfp")]
public partial class PowderSimulation : Node2D
{
	
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

	// ---------- SWAP BUFFER
	public struct Swap
	{
		public SandInfo.Cell Cell1;
		public SandInfo.Cell Cell2;

		public Swap(SandInfo.Cell cell1, SandInfo.Cell cell2)
		{
			Cell1 = cell1;
			Cell2 = cell2;
		}
		
	}

	public List<Swap> SwapQueue = new List<Swap>{};

	// ---------------------------------


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


	[ExportGroup("Appearance")]
	/// <summary>
	/// Whether the simulation appears at the center of this Node's <c>position</c> or offset like Control Nodes are.
	/// </summary>
	[Export] public bool Centered = false;
	/// <summary>
	/// How big the pixels appear on the screen.
	/// </summary>
	[Export] public int PixelScale = 5;

	// ------------------------ Follow
	[ExportGroup("Follow", "Follow")]
	
	[Export(PropertyHint.GroupEnable)]
	public bool FollowEnabled = false;
	[Export] public Node2D FollowNode;
	[Export] public Rect2 FollowAreaConstraint = new Rect2(Vector2.Zero, new Vector2(20, 20));
	[Export] public bool FollowCenterConstraintsOnFollowedNode = true;



	[ExportGroup("Debug")]
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

	[ExportGroup("")]


	// STATICS ----------------------------

	private static Vector2I[] CardinalNeighborOffsets = [Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left];

	public static readonly string DefaultFileExtension = "pwdr";


	// ------------------------------------------------------------

	public Dictionary<Vector2I, SandInfo.Chunk> Chunks = new Dictionary<Vector2I, SandInfo.Chunk>{};

	public Vector2I SimulationSize;

	public Vector2I SimulationMaxExtents;
	public Vector2I SimulationMinExtents;

	public Vector2I SimulationMaxChunkExtents;
	public Vector2I SimulationMinChunkExtents;

	// the amount of time it takes to update and draw, respectively. used for debug purposes.
	public float UpdateTime = 0.0f;
	public float DrawTime = 0.0f;
	
	// moved around various functions for testing.
	public float MiscTime = 0.0f;

	// update 60 times/sec
	private float SimDt = 1.0f / 60.0f;
	// time passed since startup
	private float Accumulator = 0.0f;
	// number of ticks passed since startup, mod by 2
	private int TicksPassed = 1;

	private WorldStreamer currentStreamer;

	public List<ChunkRendererCS> ActiveChunkRenderers = new List<ChunkRendererCS>();
	public Node2D ChunkRendererParent;

	/// <summary>
	/// 
	/// </summary>
	public Dictionary<SandInfo.Neighbors, int> NeighborIndexOffsets;

	// ------------ Signals

	[Signal]
	public delegate void ChunkAddedEventHandler(Vector2I position);
	[Signal]
	public delegate void ChunkRemovedEventHandler(Vector2I position);

	[Signal]
	public delegate void SimulationTickEventHandler();

	public event Action<Vector2I> ChunkChanged;

	// Signal Recievers ------------------------------------------

	private void OnChunkChanged(Vector2I at)
	{
		ChunkChanged?.Invoke(at);
	}

	// ***************** Setters + Getters -----------------------------------

	/// <summary>
	/// Assigns a WorldStreamer object to the simulation. When assigned, using FollowMode will save/load chunks using the WorldStreamer object. Set to <c>null</c> to disable this behavior.
	/// </summary>
	/// <param name="NewStreamer"></param>
	public void SetWorldStreamer(WorldStreamer NewStreamer)
	{
		currentStreamer = NewStreamer;
	}

	/// <summary></summary>
	/// <returns>the current WorldStreamer instance attached to this node.</returns>
	public WorldStreamer GetWorldStreamer()
	{
		return currentStreamer;
	}

	// ***************** Misc ---------------------------------------


	private ChunkRendererCS CreateChunkRenderer(Vector2I at)
	{
		ChunkRendererCS Render = new ChunkRendererCS();
		Render.ChunkSize = IndividualChunkSize;
		Render.PixelScale = PixelScale;
		Render.Position = at * IndividualChunkSize * PixelScale;
		if (DebugMode)
		{
			Render.ShowDebugInfo = true;
		}

		ChunkRendererParent.AddChild(Render);
		ActiveChunkRenderers.Add(Render);

		return Render;
	}

	private void RemoveChunkRenderer(ChunkRendererCS renderer)
	{
		ActiveChunkRenderers.Remove(renderer);
		renderer.QueueFree();	
	}

	/// <summary></summary>
	/// <param name="pos"></param>
	/// <returns>The chunk currently at <c>pos</c>. Throws an error if there is no chunk at <c>pos</c>.</returns>
	public SandInfo.Chunk GetChunk(Vector2I pos)
	{
		//old array-based chunk lookup
		//return Chunks[pos.Y * ChunkGridSize.X + pos.X];

		return Chunks[pos];
	}

	/// <summary></summary>
	/// <param name="pos"></param>
	/// <returns>Whether the given <c>pos</c> corresponds to a cell in the simulation.</returns>
	public bool SimContainsCell(Vector2I pos)
	{
		//return !(pos.X < SimulationMinExtents.X || pos.Y < SimulationMinExtents.Y
		//	|| pos.X > SimulationMaxExtents.X || pos.Y > SimulationMaxExtents.Y);
		return Chunks.TryGetValue(LocalToChunk(pos), out SandInfo.Chunk chunk);
	}

	/// <summary></summary>
	/// <param name="pos"></param>
	/// <returns>Whether the given <c>pos</c> corresponds to a chunk in the simulation.</returns>
	public bool SimContainsChunk(Vector2I pos)
	{
		//return !(pos.X < SimulationMinChunkExtents.X || pos.Y < SimulationMinChunkExtents.Y
		//	|| pos.X > SimulationMaxChunkExtents.X || pos.Y > SimulationMaxChunkExtents.Y);
		return Chunks.TryGetValue(pos, out SandInfo.Chunk chunk);
	}



	// ******************* Automagic chunk loading section with magic did i mention that already pretty magical am i right or am i right with the whole magic thing its pretty cool right i swear it is please i need this pleaseeeeeeeee


	private bool PointInsideConstraints(Vector2 LocalPoint)
	{
		return FollowAreaConstraint.HasPoint(LocalPoint);
	}

	/// <summary>
	/// Sets <c>FollowEnabled</c> to <c>to</c>.
	/// </summary>
	/// <param name="to"></param>
	public void SetFollowMode(bool to)
	{
		FollowEnabled = to;
	}

	/// <summary>
	/// Sets the node for the simulation to follow. If <c>FollowEnabled</c> is set to <c>true</c>, the simulation uses the Node2D's position to determine where
	/// the constraints are centered.
	/// </summary>
	/// <param name="what"></param>
	public void SetFollowNode(Node2D what)
	{
		FollowNode = what;
	}

	/// <summary>
	/// Sets the constraints for the simulation. If <c>FollowEnabled</c> is set to <c>true</c>, the simulation auto loads/unloads chunks to fill the range ± <c>to</c>.
	/// </summary>
	/// <param name="to"></param>
	public void SetConstraints(Vector2 to)
	{
		FollowAreaConstraint = new Rect2(Vector2.Zero, to);

		if (FollowCenterConstraintsOnFollowedNode)
		{
			FollowAreaConstraint.Position = -(FollowAreaConstraint.Size / 2.0f);
		}
	}

	// alright actual magic part i swear ------- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -


	/// <summary>
	/// Deletes all chunks outside and adds chunks inside of the constraints defined in <c>FollowAreaConstraints</c> around <c>FollowNode.Position</c>. 
	/// If a WorldStreamer is set using <see cref="SetWorldStreamer"/>, it uses that file to load and save chunks.
	/// </summary>
	private void ConstrainChunks()
	{
		// shrink first to reduce number of checks needed in GrowChunks

		Vector2I[] chunkPositions = new Vector2I[Chunks.Count];
		Chunks.Keys.CopyTo(chunkPositions, 0);

		foreach(Vector2I cPosition in chunkPositions)
		{
			ShrinkChunk(cPosition);

			GrowChunk(cPosition);
		}
	}

	/// <summary>
	/// Checks all chunks grown outward by one chunk to see if they need to be added to the world.
	/// </summary>
	private void GrowChunk(Vector2I pos)
	{
		// loop over neighbors to check if they need to be added
		foreach(Vector2I offset in CardinalNeighborOffsets)
		{
			
			if(Chunks.Keys.Contains<Vector2I>(pos + offset))
			{
				continue;
			}

			// find the position to check in constraints, with offset and world transforms
			Vector2 targetPos = ChunkLocalToWorld(pos + offset, false) - FollowNode.Position;
			
			if (PointInsideConstraints(targetPos))
			{
				// add chunk, load chunk if worldStreamer is set
				if(currentStreamer != null)
				{
					// if chunk does not exist in file, add new chunk
					if(!currentStreamer.LoadChunk(pos + offset))
					{
						AddChunk(pos + offset);
					}				
				}
				else
				{
					AddChunk(pos + offset);
				}
				
			}
			

		}
	}

	/// <summary>
	/// Checks specified chunk to find if it needs to be removed.
	/// </summary>
	private void ShrinkChunk(Vector2I pos)
	{
		// get the chunk position offset by the FollowNode's position, since the constraining Rect2 is centered at the origin
		Vector2 testPosition = ChunkLocalToWorld(pos, false) - FollowNode.Position;
		// only deletes chunks that aren't inside the constraints
		if (PointInsideConstraints(testPosition))
		{
			return;
		}

		// unload chunk if worldstreamer is set
		if (currentStreamer != null)
		{
			currentStreamer.UnloadChunk(GetChunk(pos));
		}
		else
		{
			RemoveChunk(pos);
		}
		
	
	}


	// **************** Helpers ----------------------------------------------


	/// <summary></summary>
	/// <param name="from_world"></param>
	/// <returns>the given cell world position (Godot's regular <c>global_position</c> for nodes) translated into local simulation position.</returns>
	public Vector2I WorldToLocal(Vector2 from_world)
	{
		return (Vector2I)from_world / PixelScale - (Vector2I)(Position / PixelScale);
	}

	/// <summary></summary>
	/// <param name="fromLocal"></param>
	/// <param name="includeParentOffset"></param>
	/// <returns>the given cell local simulation position translated into world position.</returns>
	public Vector2 LocalToWorld(Vector2I fromLocal, bool includeParentOffset = true)
	{
		return fromLocal * PixelScale + (Position * (includeParentOffset ? 1 : 0));
	}

	/// <summary></summary>
	/// <param name="fromLocal"></param>
	/// <param name="includeParentOffset"></param>
	/// <returns>the given chunk local simulation position translated into world position.</returns>
	public Vector2 ChunkLocalToWorld(Vector2I fromLocal, bool includeParentOffset = true)
	{
		Vector2 centerOffset = new Vector2(IndividualChunkSize, IndividualChunkSize) / 2.0f * PixelScale;

		return fromLocal * PixelScale * IndividualChunkSize + centerOffset + (Position * (includeParentOffset ? 1 : 0));
	}

	/// <summary></summary>
	/// <returns>the converted local simulation coordinates to chunk coordinates.</returns>
	public Vector2I LocalToChunk(Vector2I localPos)
	{
		return new Vector2I(
			(int)Math.Floor((decimal)localPos.X / (decimal)IndividualChunkSize),
			(int)Math.Floor((decimal)localPos.Y / (decimal)IndividualChunkSize)
		);
	}

	/// <summary></summary>
	/// <returns>the given global (world) position converted to chunk coordinates.</returns>
	public Vector2I WorldToChunk(Vector2 worldPos)
	{
		return LocalToChunk(WorldToLocal(worldPos));
	}


	// ***************** Swap Buffer ----------------------------------------

	/// <summary>
	/// Adds two cells to list of cells to be swapped at the end of the frame.
	/// </summary>
	/// <param name="Cell1"></param>
	/// <param name="Cell2"></param>
	public void QueueSwap(SandInfo.Cell Cell1, SandInfo.Cell Cell2)
	{
		SwapQueue.Add(new Swap(Cell1, Cell2));
	}

	/// <summary>
	/// Writes all the buffered swaps onto the simulation.
	/// </summary>
	public void CommitSwapQueue()
	{
		foreach(Swap swap in SwapQueue)
		{
			SandInfo.SwapCells(swap.Cell1, swap.Cell2);
		}

		SwapQueue.Clear();
	}


	// ***************** Chunk Addition + Removal ----------------------------


	/// <summary>
	/// Adds an empty chunk at the specified coordinates.
	/// </summary>
	/// <param name="at"></param>
	/// <returns>Whether or not the chunk was successfully added.</returns>
	public bool AddChunk(Vector2I at)
	{
		if(Chunks.TryAdd(at, new SandInfo.Chunk(IndividualChunkSize, at, ChunkInsomnia))){
			EmitSignal(SignalName.ChunkAdded, at);

			GetChunk(at).OnChange += OnChunkChanged;

			UpdateSimulationSize();
			Chunks[at].Renderer = CreateChunkRenderer(at);

			UpdateAdjacent(at);
			return true;
		}
		else
		{
			return false;
		}
		
	}

	/// <summary>
	/// Removes the chunk at the specified coordinates.
	/// </summary>
	/// <param name="at"></param>
	/// <returns>Whether there was a chunk at the specified position or not.</returns>
	public bool RemoveChunk(Vector2I at)
	{
		if (Chunks.TryGetValue(at, out SandInfo.Chunk chunk))
		{
			EmitSignal(SignalName.ChunkRemoved, at);

			RemoveChunkRenderer(chunk.Renderer);
			
			// remove from signal group
			GetChunk(at).OnChange -= OnChunkChanged;

			Chunks.Remove(at);
			UpdateSimulationSize();

			return true;
		}
		else
		{
			return false;
		}

	}

	/// <summary>
	/// Wakes and resets the dirty rect from the chunks adjacent to the one at <c>from</c>.
	/// </summary>
	/// <param name="from"></param>
	private void UpdateAdjacent(Vector2I from)
	{
		// + (0, -1)
		if(Chunks.TryGetValue(from + new Vector2I(0, -1), out SandInfo.Chunk uChunk))
		{
			uChunk.ResetDirtyRect();
			uChunk.Wake();
		}

		// + (0, 1)
		if(Chunks.TryGetValue(from + new Vector2I(0, 1), out SandInfo.Chunk dChunk))
		{
			dChunk.ResetDirtyRect();
			dChunk.Wake();
		}

		// + (-1, 0)
		if(Chunks.TryGetValue(from + new Vector2I(-1, 0), out SandInfo.Chunk lChunk))
		{
			lChunk.ResetDirtyRect();
			lChunk.Wake();
		}

		// + (1, 0)
		if(Chunks.TryGetValue(from + new Vector2I(1, 0), out SandInfo.Chunk rChunk))
		{
			rChunk.ResetDirtyRect();
			rChunk.Wake();
		}
		
	}

	// ***************** Updating + Initialization --------------------------

	/// <summary>
	/// Populates the grid with Chunk and Cell objects set to the correct default state. One of the functions called in this node's _Ready() function.
	/// </summary>
	public void InitGrid()
	{
		FindIndexOffsets();

		Chunks.Clear();

		// if centered, find offset
		Vector2I offset = new Vector2I(0, 0);

		if (Centered)
		{
			offset.X = (int)Math.Floor((decimal)ChunkGridSize.X / 2);
			offset.Y = (int)Math.Floor((decimal)ChunkGridSize.Y / 2);
		}

		// create grid
		for(int y = 0; y < ChunkGridSize.Y; y++)
		{
			for(int x = 0; x < ChunkGridSize.X; x++)
			{
				Vector2I LoopPos = new Vector2I(x, y) - offset;

				//int ChunkListPos = y * ChunkGridSize.X + x;

				AddChunk(LoopPos);

				Chunks[LoopPos].Renderer = CreateChunkRenderer(LoopPos);
			}
		}

		UpdateSimulationSize();
	}

	private void FindIndexOffsets()
	{
		NeighborIndexOffsets = new Dictionary<SandInfo.Neighbors, int>
		{
			{SandInfo.Neighbors.TOPLEFT, -(IndividualChunkSize + 1)},
			{SandInfo.Neighbors.TOPMIDDLE, -IndividualChunkSize},
			{SandInfo.Neighbors.TOPRIGHT, -(IndividualChunkSize - 1)},
			{SandInfo.Neighbors.LEFTMIDDLE, -1},
			{SandInfo.Neighbors.RIGHTMIDDLE, 1},
			{SandInfo.Neighbors.BOTTOMLEFT, IndividualChunkSize - 1},
			{SandInfo.Neighbors.BOTTOMMIDDLE, IndividualChunkSize},
			{SandInfo.Neighbors.BOTTOMRIGHT, IndividualChunkSize + 1},	
		};
	}

	/// <summary>
	/// Updates the cells in all the chunks of the simulation, sending them to the swap buffer. Automatically called in UpdateSimulation().
	/// </summary>
	/// <param name="tick">The elapsed frame, mod by two (constrained to 0 and 1). Changes update direction based on the current tick, making the simulation less one-sided.</param>
	public void UpdateChunks(int tick)
	{
		if(tick == 1){

			Vector2I pos = new Vector2I(0, 0);
			for(int y = SimulationMinChunkExtents.Y; y <= SimulationMaxChunkExtents.Y; y++)
			{
				for(int x = SimulationMinChunkExtents.X; x <= SimulationMaxChunkExtents.X; x++)
				{
					pos.X = x;
					pos.Y = y;
					Chunks.TryGetValue(pos, out SandInfo.Chunk chunk);
					if(chunk != null)
					{
						chunk.UpdateCells(this, tick);
					}
				}
			}

		}
		else
		{
			
			Vector2I pos = new Vector2I(0, 0);
			for(int y = SimulationMaxChunkExtents.Y; y >= SimulationMinChunkExtents.Y; y--)
			{
				for(int x = SimulationMaxChunkExtents.X; x >= SimulationMinChunkExtents.X; x--)
				{
					pos.X = x;
					pos.Y = y;
					Chunks.TryGetValue(pos, out SandInfo.Chunk chunk);
					if(chunk != null)
					{
						chunk.UpdateCells(this, tick);
					}
				}
			}
		}

		if (UseDirtyRects)
		{
			foreach(SandInfo.Chunk chunk in Chunks.Values)
			{
				chunk.UpdateDirtyRect();
			}
		}

		if(DebugMode || ShowSimBorder)
		{
			QueueRedraw();
		}
	}

	/// <summary>
	/// <b>DOES NOT WORK</b> Equivalent to <c>UpdateChunks(int tick)</c> except instead of looping over a chunk's cells, then the next one, etc., it instead loops over the whole simulation like it's a single chunk.
	/// </summary>
	public void UpdateCells(int tick)
	{
		if(tick == 1){
			
			Vector2I CellPos = new Vector2I(0, 0);

			for(int y = 0; y < SimulationSize.Y; y++)
			{
				for(int x = 0; x < SimulationSize.X; x++)
				{
					CellPos.X = x;
					CellPos.Y = y;					

					SandInfo.Cell cell = GetCell(CellPos);

					cell.CellChunk.UpdateCell(cell, this);

				}
			}

		}
		else
		{
			
			Vector2I CellPos = new Vector2I(0, 0);

			for(int y = SimulationSize.Y; y >= 0; y--)
			{
				for(int x = SimulationSize.X; x >= 0; x--)
				{
					CellPos.X = x;
					CellPos.Y = y;					

					SandInfo.Cell cell = GetCell(CellPos);

					cell.CellChunk.UpdateCell(cell, this);

				}
			}
		}

		if (UseDirtyRects)
		{
			foreach(SandInfo.Chunk chunk in Chunks.Values)
			{
				chunk.UpdateDirtyRect();
				chunk.DecideSleepState();
			}
		}
		else
		{
			DecideSleepingChunks();	
		}

		if(DebugMode)
		{
			QueueRedraw();
		}
	}

	/// <summary>
	/// Should only need to be called after <c>UpdateCells(int tick)</c> is finished, <b>NOT</b> <c>UpdateChunks(int tick)</c>.
	/// it is automatically called in <c>UpdateCells(int tick)</c>.
	/// </summary>
	public void DecideSleepingChunks()
	{
		foreach(SandInfo.Chunk chunk in Chunks.Values)
		{
			chunk.DecideSleepState();
		}
	}

	/// <summary>
	/// Loops through each chunk and sends its cell data to its respective ChunkRenderer.
	/// </summary>
	public void RenderChunkUpdates()
	{
		DrawTime = Time.GetTicksUsec() / 1000.0f;
		// -----------------------------------------------------------------
		foreach(SandInfo.Chunk chunk in Chunks.Values)
		{
			chunk.SendDrawInfoToRenderer();
		}
		// -----------------------------------------------------------------
		DrawTime = Time.GetTicksUsec() / 1000.0f - DrawTime;
	}


	/// <summary>
	/// Updates the simulation_size variable if chunk grid size has been changed. This is automatically done, so no need to call this.
	/// </summary>
	public void UpdateSimulationSize()
	{

		if(Chunks.Count == 0)
		{
			SimulationSize = new Vector2I(ChunkGridSize.X * IndividualChunkSize - 1, ChunkGridSize.Y * IndividualChunkSize - 1);
			SimulationMaxExtents = Vector2I.Zero;
			SimulationMinExtents = Vector2I.Zero;
			SimulationMaxChunkExtents = Vector2I.Zero;
			SimulationMinChunkExtents = Vector2I.Zero;
			return;
		}


		SimulationMaxExtents = new Vector2I(int.MinValue, int.MinValue);
		SimulationMinExtents = new Vector2I(int.MaxValue, int.MaxValue);

		SimulationMaxChunkExtents = new Vector2I(int.MinValue, int.MinValue);
		SimulationMinChunkExtents = new Vector2I(int.MaxValue, int.MaxValue);

		foreach(SandInfo.Chunk chunk in Chunks.Values)
		{
			SimulationMinExtents.X = Math.Min(SimulationMinExtents.X, chunk.MinExtents.X);
			SimulationMinExtents.Y = Math.Min(SimulationMinExtents.Y, chunk.MinExtents.Y);

			SimulationMaxExtents.X = Math.Max(SimulationMaxExtents.X, chunk.MaxExtents.X);
			SimulationMaxExtents.Y = Math.Max(SimulationMaxExtents.Y, chunk.MaxExtents.Y);

			// chunks
			SimulationMinChunkExtents.X = Math.Min(SimulationMinChunkExtents.X, chunk.ChunkPosition.X);
			SimulationMinChunkExtents.Y = Math.Min(SimulationMinChunkExtents.Y, chunk.ChunkPosition.Y);

			SimulationMaxChunkExtents.X = Math.Max(SimulationMaxChunkExtents.X, chunk.ChunkPosition.X);
			SimulationMaxChunkExtents.Y = Math.Max(SimulationMaxChunkExtents.Y, chunk.ChunkPosition.Y);

		}

		SimulationSize = SimulationMaxExtents - SimulationMinExtents;

	}

	public override void _Ready()
	{

		SetConstraints(FollowAreaConstraint.Size);

		ChunkRendererParent = new Node2D();
		ChunkRendererParent.Name = "ChunkRendererParent";
		AddChild(ChunkRendererParent);

		InitGrid();

		
	}

	// used to draw dirty rects, chunk borders and the sim border for debug purposes.
    public override void _Draw()
    {
        base._Draw();

		// draw sim border
		if(ShowSimBorder)
		{
			// position offset created by how big the width of the border is
			Vector2 widthOffset = new Vector2(-SimBorderWidth / 2, -SimBorderWidth / 2);

			DrawRect(new Rect2(SimulationMinExtents * PixelScale + widthOffset, SimulationSize * PixelScale + new Vector2(PixelScale + SimBorderWidth, PixelScale + SimBorderWidth)), new Color(0, 0, 0, 1.0f), false, SimBorderWidth, false);
		}


		if (!DebugMode)
		{
			return;
		}

		//dirty rects
		Vector2I CenteringVal = new Vector2I(PixelScale, PixelScale);
		foreach(SandInfo.Chunk chunk in Chunks.Values)
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

	/// <summary>
	/// Updates the simulation while being safe to put in <c>_Process</c>/<c>_PhysicsProcess</c> because if <c>wait</c> is set to <c>true</c>, it will not tick until the right amount of time has passed specified in <c>SimulationSpeed</c>.
	/// If <c>wait</c> is set to <c>false</c>, runs one tick of the simulation regardless.
	/// </summary>
	/// <param name="wait">Whether or not to check if the correct amount of time has passed specified in `SimulationSpeed`, and if not, does not call the function.</param>
	public void UpdateSimulation(bool wait = true)
	{
		
		Accumulator += (float)GetProcessDeltaTime() * SimulationSpeed;

		if(wait == false || Accumulator >= SimDt)
		{
			UpdateTime = Time.GetTicksUsec() / 1000.0f;
			// ----------------------------------------------------------------------------

			// emits signal when updated
			EmitSignal(SignalName.SimulationTick);

			// constrains chunks if FollowEnabled is true 
			if (FollowEnabled && TicksPassed == 1)
			{
				ConstrainChunks();
			}

			UpdateChunks(TicksPassed);
			CommitSwapQueue();
			//-----------------------------------------------------------------------------
			UpdateTime = Time.GetTicksUsec() / 1000.0f - UpdateTime;
			RenderChunkUpdates();

			TicksPassed += 1;
			TicksPassed %= 2;
			Accumulator = 0;
		}

	}


	// ************************ World Manipulation -----------------------------------

	/// <summary>
	/// 
	/// </summary>
	/// <param name="pos"></param>
	/// <returns>The cell at the specified <c>pos</c>.</returns>
	public SandInfo.Cell GetCell(Vector2I pos)
	{
		Vector2I chunkPos = LocalToChunk(pos);

		//SandInfo.Chunk chunk = Chunks[chunkPosY * ChunkGridSize.X + chunkPosX];
		SandInfo.Chunk chunk = Chunks[new Vector2I(chunkPos.X, chunkPos.Y)];

		int localX = pos.X - chunk.MinExtents.X;
		int localY = pos.Y - chunk.MinExtents.Y;

		return chunk.Cells[localY * IndividualChunkSize + localX];

	}

	/// <summary>
	/// Replaces the element at <c>pos</c> with <c>element</c>.
	/// </summary>
	/// <param name="pos"></param>
	/// <param name="element"></param>
	public void SetCell(Vector2I pos, AllElements element)
	{
		SetCell(GetCell(pos), element);
	}

	public void SetCell(SandInfo.Cell cell, AllElements element)
	{
		cell.Element = element;

		cell.ReplaceTypeAttributesWithDefault();

		cell.CellChunk.Wake();
		cell.CellChunk.MarkCellUpdated(cell, false);
	}

	public bool PlaceElement(Vector2I pos, AllElements element, bool overRide = false)
	{
		if(!SimContainsCell(pos))
		{
			return false;
		}
		

		SandInfo.Cell cell = GetCell(pos);

		if(overRide == false && cell.Element != AllElements.AIR)
		{
			return false;
		}
		else
		{
			SetCell(cell, element);
			return true;
		}
	}


	public void PlaceGroupElements(int brushSize, Vector2I pos, AllElements element, bool overRide = false)
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
	/// replaces every element in the current world with the element <c>with</c>.
	/// </summary>
	/// <param name="with"></param>
	public void FillWorld(AllElements with)
	{
		foreach(SandInfo.Chunk chunk in Chunks.Values)
		{
			chunk.ReplaceAll(with);
		}
	}

	/// <summary>
	/// Replaces every element in the current world with <c>AIR</c>. Equivalent to calling <c>FillWorld(AllElements.AIR)</c>.
	/// </summary>
	public void ClearWorld()
	{
		foreach(SandInfo.Chunk chunk in Chunks.Values)
		{
			chunk.ClearAll();
		}
	}


}
