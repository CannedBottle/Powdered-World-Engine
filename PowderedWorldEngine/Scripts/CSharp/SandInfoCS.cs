using Godot;
using System;
using System.Collections.Generic;

public partial class SandInfoCS : Node
{
	public enum Elements
	{
		AIR,
		SAND,
		WATER,
		ACID,
		STONE,
		WALL
	}

	// a way of determining what special attributes an element has.
	public enum ElementTypes
	{
		STATIC,
		SOLID,
		LIQUID,
		GAS
	}

	public static readonly Dictionary<Elements, ElementTypes> ElementToType = new Dictionary<Elements, ElementTypes>
	{
		{Elements.AIR, ElementTypes.STATIC},
		{Elements.SAND, ElementTypes.SOLID},
		{Elements.WATER, ElementTypes.LIQUID},
		{Elements.ACID, ElementTypes.LIQUID},
		{Elements.STONE, ElementTypes.SOLID},
		{Elements.WALL, ElementTypes.STATIC}
	};


	// in the random values, a Vector2i is used for min/max values since constants cannot use random functions.
	public static readonly Dictionary<ElementTypes, Dictionary<string, int>> ElementTypeDefaults = new Dictionary<ElementTypes, Dictionary<string, int>>
	{
		{ElementTypes.STATIC, new Dictionary<string, int>{{"none", 0}}},
		
		{ElementTypes.SOLID, new Dictionary<string, int>{{"random", 1}}},
		
		{ElementTypes.LIQUID, new Dictionary<string, int> 
		{
			{"random", 1},
			{"direction", 0},
			{"density", 0},
		}},
		
		{ElementTypes.GAS, new Dictionary<string, int>{{"random", 7}}},
	};


	public static readonly Dictionary<Elements, Vector4> BaseElementColors = new Dictionary<Elements, Vector4>
	{
		{Elements.AIR, Vector4.Zero},
		{Elements.SAND, new Vector4(1.0f, 0.93f, 0.474f, 1.0f)},
		{Elements.WATER, new Vector4(0.112f, 0.644f, 0.93f, 0.6f)},
		{Elements.ACID, new Vector4(0.678f, 1.0f, 0.31f, 0.75f)},
		{Elements.STONE, new Vector4(0.58f, 0.58f, 0.58f, 1.0f)},
		{Elements.WALL, new Vector4(0.27f, 0.27f, 0.27f, 1.0f)},
	};


	// ------------------------------------ USEFUL FUNCTIONS ----------------------------- //
	public static int WorldPosToChunkPos(Vector2I worldPos, int chunkSize, float inverseChunkSize)
	{
		Vector2I ChunkPos = new Vector2I((int)(worldPos.X * inverseChunkSize), (int)(worldPos.Y * inverseChunkSize));
		int LocalX = worldPos.X - (ChunkPos.X * chunkSize);
		int LocalY = worldPos.Y - (ChunkPos.Y * chunkSize);
		return LocalY * chunkSize + LocalX;
	}


	// --------------------------------- ELEMENT MOVEMENT RULESETS ----------------------- //

	public static void OnUpdate(Cell cell)
	{
		if(cell.CellType == ElementTypes.LIQUID)
		{
			if(cell.TryMove("bottommiddle") == false)
			{
				bool success = false;
				if(cell.TypeAttributes["random"] == 0)
				{
					success = cell.TryMove("bottomright");
					if(success == false)
					{
						success = cell.TryMove("bottomleft");
					}
				}
				else
				{
					success = cell.TryMove("bottomleft");
					if(success == false)
					{
						success = cell.TryMove("bottomright");
					}
				}

				if(success == false)
				{
					if(cell.TypeAttributes["direction"] == 1)
					{
						success = cell.TryMove("rightmiddle");
						cell.TypeAttributes["direction"] = success ? 1 : 0;

						if(success == false)
						{
							cell.TryMove("leftmiddle");
						}
					}
					else
					{
						success = cell.TryMove("leftmiddle");
						cell.TypeAttributes["direction"] = success ? 0 : 1;

						if(success == false)
						{
							cell.TryMove("rightmiddle");
						}
					}
				}
			}
		}
		else
		{
			switch (cell.Element)
			{
				case Elements.SAND:
					if(cell.TryMove("bottommiddle") == false)
					{
						if(cell.TypeAttributes["random"] == 0)
						{
							if(cell.TryMove("bottomright") == false)
							{
								cell.TryMove("bottomleft");
							}
						}
						else
						{
							if(cell.TryMove("bottomleft") == false)
							{
								cell.TryMove("bottomright");
							}
						}
					}
					break;
			}

		}
	}




	// --------------------------------- CELL / CHUNK CLASSES ----------------------------- //

	public class Cell
	{
		
		public Vector2I Position;
		public ElementTypes CellType;
		public Elements Element
		{
			get => Element;
			set
			{
				Element = value;
				CellType = ElementToType[value];

				if(CellType == ElementTypes.LIQUID)
				{
					Brightness = 1.0f;
				}
				else
				{
					Brightness = (float)GD.RandRange(0.9, 1.0);
				}

				ReplaceTypeAttributesWithDefault(CellType);
			}
		}


		public Dictionary<string, int> TypeAttributes;
		public Dictionary<string, Vector2I> Neighbors; // if neighbor is an edge, will show up as (-1, -1)
		public readonly List<string> NeighborStrings = [
			"topleft", "topmiddle", "topright", "leftmiddle", "rightmiddle", "bottomleft", "bottommiddle", "bottomright"
		];

		public PowderSimulationCs SimRef;
		public float Brightness;
		public int ChunkSize;
		public Chunk CellChunk;
		public Vector2I SimSize;

		// idx of cell inside the chunk's own cells list, basically local position
		public int ChunkIdx;

		// if on the edge of a chunk, so can decide whether to update a chunk beside it / cross over to another chunk
		public bool ChunkEdge = false;
		public bool SimEdge = false;



		public Cell(Vector2I cellPosition, int chunkSize, Elements cellElement, PowderSimulationCs simRef, Chunk cellChunk, int cellChunkIdx)
		{
			Position = cellPosition;
			ChunkSize = chunkSize;
			Element = cellElement;
			SimRef = simRef;
			CellChunk = cellChunk;
			ChunkIdx = cellChunkIdx;

			SimSize = SimRef.SimulationSize;


			ChunkEdge = Position.X == CellChunk.MaxExtents.X || Position.Y == CellChunk.MaxExtents.Y || Position.X == CellChunk.MinExtents.X || Position.Y == CellChunk.MinExtents.Y;

			SimEdge = Position.X == 0 || Position.Y == 0 || Position.X == SimSize.X || Position.Y == SimSize.Y;
		}


		private void ReplaceTypeAttributesWithDefault(ElementTypes newType)
		{
			TypeAttributes = new Dictionary<string, int>(ElementTypeDefaults[newType]);

			if(TypeAttributes.TryGetValue("random", out int randVal) == true)
			{
				TypeAttributes["random"] = GD.RandRange(0, randVal);
			}

			if(TypeAttributes.TryGetValue("direction", out int directVal) == true)
			{
				TypeAttributes["direction"] = TypeAttributes["random"];
			}
		}

		public void FindNeighborIndices()
		{
			int LoopNum = 0;

			for(int y = -1; y == 1; y++)
			{
				for(int x = -1; x == 1; x++)
				{
					if(x == 0 && y == 0)
					{
						continue;
					}

					Neighbors.Add(NeighborStrings[LoopNum], new Vector2I(Position.X + x, Position.Y + y));


					// edge checking
					if(Position.X + x > SimSize.X || Position.X + x < 0 ||
					Position.Y + y > SimSize.Y || Position.Y + y < 0)
					{
						Neighbors[NeighborStrings[LoopNum]] = new Vector2I(-1, -1);
					}


					LoopNum += 1;
				}
			}
		}


		// ---------------------------------======= Movement Stuff =======--------------------------------------- //
		public bool CanMove(Cell NeighborCell)
		{
			bool ValidMove = false;

			switch (CellType)
			{
				case ElementTypes.SOLID:
					ValidMove = NeighborCell.CellType == ElementTypes.LIQUID || NeighborCell.CellType == ElementTypes.GAS;
					break;
			}

			return ValidMove;
		}


		///returns whether the move that successful or not.
		public bool TryMove(string ToNeighbor)
		{
			Vector2I NeighborPos = Neighbors[ToNeighbor];
			if(NeighborPos == Vector2I.Zero)
			{
				return false;
			}

			Chunk WriteChunk = CellChunk;

			if (ChunkEdge)
			{
				Vector2I NewChunkPos = new Vector2I((int)(NeighborPos.X * CellChunk.InvChunkSize), (int)(NeighborPos.Y * CellChunk.InvChunkSize));

				WriteChunk = SimRef.Chunks[NewChunkPos];
			}

			int NeighborIdx = WorldPosToChunkPos(NeighborPos, ChunkSize, CellChunk.InvChunkSize);

			//valid cell to move checking (per type check)
			bool ValidMove = false;
			Cell NeighborCell = WriteChunk.Cells[NeighborIdx];
			if(NeighborCell.Element == Elements.AIR)
			{
				ValidMove = true;
			}
			else
			{
				ValidMove = CanMove(NeighborCell);
			}


			if (ValidMove)
			{
				
				CellChunk.MarkCellUpdated(this);
				WriteChunk.MarkCellUpdated(NeighborCell);
				

				// if on edge of chunk and successfully updates, wake chunk next to it
				if(ChunkEdge && !SimEdge)
				{
					//wake chunk nearest to cell   (no bool to int now i have to use ternary ops :[ )
					Vector2I PosAddition = new Vector2I(
						(Position.X == CellChunk.MaxExtents.X ? 1 : 0) - (Position.X == CellChunk.MinExtents.X ? 1 : 0),
						(Position.Y == CellChunk.MaxExtents.Y ? 1 : 0) - (Position.Y == CellChunk.MinExtents.Y ? 1 : 0)
					);

					if(PosAddition != Vector2.Zero)
					{
						SimRef.Chunks[CellChunk.ChunkPosition + PosAddition].Wake();
						//TODO: PUT CODE TO UPDATE THE DIRTYRECT OF THE CHUNK HERE LATER
						// could use the y position of the cell to mark that one + their neighbors for updating in dirtyrect, or just update that whole side
					}
				}

				CellChunk.SwapCells(this, NeighborCell);
				WriteChunk.Wake();
				
				return true;
			}
			else
			{
				return false;
			}
			
		}
	}



	public class Chunk
	{
		
		public int ChunkSize;
		//reference to simulation node
		public PowderSimulationCs SimRef;

		//how many updates of no change before sleeping
		public int Insomnia = 1;
		//keeps track of updates of no change for insomnia
		public int InsomniaCount = 0;
		public List<Cell> Cells;
		//cells to draw, also cells that have been updated in the past frame
		public bool[] UpdatedCellsMask;
		public List<int> UpdatedCellsIndexes;

		//position of chunk in the grid of chunks (not cells)
		public Vector2I ChunkPosition;

		// the max/min values the chunk cells dict contains, used for moving cells from one chunk to another
		public Vector2I MaxExtents;
		public Vector2I MinExtents;


		// ----- dirty rect vars ----- //
		public Vector2I DirtyRectMax;
		public Vector2I DirtyRectMin;
		public bool HasValidDirtyRect;

		// to remove unneccesary division operations
		public float InvChunkSize;

		public bool Sleeping = false;

		public ChunkRendererCS Renderer;

		public Chunk(int chunkSize, Vector2I chunkPosition, PowderSimulationCs simRef, int insomnia)
		{
			ChunkSize = chunkSize;
			ChunkPosition = chunkPosition;
			SimRef = simRef;
			Insomnia = insomnia;

			InvChunkSize = 1.0f / ChunkSize;
			UpdatedCellsMask = new bool[ChunkSize * ChunkSize];
			

			// set extents
			MaxExtents = new Vector2I(
				ChunkPosition.X * ChunkSize + ChunkSize - 1,
				ChunkPosition.Y * ChunkSize + ChunkSize - 1
			);
			MinExtents = new Vector2I(
				ChunkPosition.X * ChunkSize,
				ChunkPosition.Y * ChunkSize
			);

			DirtyRectMax = MaxExtents;
			DirtyRectMin = MinExtents;

			InitCells();

		}


		public void InitCells()
		{
			int Idx = 0;
			for(int y = 0; y < ChunkSize; y++)
			{
				for(int x = 0; x < ChunkSize; x++)
				{
					
					// find position in all of the cells, not just local chunk
					Vector2I Pos = new Vector2I(
						x + MinExtents.X,
						y + MinExtents.Y
					);

					Cells.Add(new Cell(Pos, ChunkSize, Elements.AIR, SimRef, this, Idx));

					Idx += 1;
				}
			}

			foreach (Cell cell in Cells)
			{
				cell.FindNeighborIndices();
			}

		}


		public void UpdateCells()
		{
			if (Sleeping)
			{
				// TODO: Modulate cell border if sleeping
				return;
			}else if (Renderer.ShowDebugInfo)
			{
				// TODO: Modulate if not sleeping
			}


			foreach (Cell cell in Cells)
			{
				if (HasValidDirtyRect)
				{
					if(!(cell.Position <= DirtyRectMax + new Vector2I(2, 2) && cell.Position >= DirtyRectMin - new Vector2I(2, 2)))
					{
						// inflate over chunk in the place where cell updates chunk when on edge
						continue;
					}
				}

				if(UpdatedCellsMask[cell.ChunkIdx] == true || cell.Element == Elements.AIR)
				{
					continue;
				}

				OnUpdate(cell);

			}

			//sleeps chunk if no cells are updated
			if(UpdatedCellsIndexes.Count == 0)
			{
				if (InsomniaCount >= Insomnia - 1)
				{
					InsomniaCount = 0;
					Sleep();
				}
				else
				{
					InsomniaCount += 1;
				}

			}
			else
			{
				InsomniaCount = 0;
			}

		}

		public void MarkCellUpdated(Cell cell)
		{
			UpdatedCellsIndexes.Add(cell.ChunkIdx);
			UpdatedCellsMask[cell.ChunkIdx] = true;
		}


		public void UpdateDirtyRect()
		{
			HasValidDirtyRect = false;
			DirtyRectMax = MinExtents;
			DirtyRectMin = MaxExtents;

			foreach(int idx in UpdatedCellsIndexes)
			{
				Vector2I Pos = Cells[idx].Position;
				DirtyRectMax.X = Math.Max(DirtyRectMax.X, Pos.X);
				DirtyRectMax.Y = Math.Max(DirtyRectMax.Y, Pos.Y);

				DirtyRectMin.X = Math.Min(DirtyRectMin.X, Pos.X);
				DirtyRectMin.Y = Math.Min(DirtyRectMin.Y, Pos.Y);
			}

			if(DirtyRectMax != MinExtents && DirtyRectMin != MaxExtents)
			{
				HasValidDirtyRect = true;
			}

		}


		public void SendDrawInfoToRenderer()
		{
			if(UpdatedCellsIndexes.Count == 0)
			{
				return;
			}

			foreach(int idx in UpdatedCellsIndexes)
			{
				Cell cell = Cells[idx];
				//base element color
				Vector4 Col = BaseElementColors[cell.Element];
				//darkness of pixel
				float D = 1.0f - cell.Brightness;
				//darkness applied to only non-alpha channels
				Renderer.CellValues[cell.ChunkIdx] = new Vector4(Col.X - D, Col.Y - D, Col.Z - D, Col.W);
			}
			UpdatedCellsIndexes.Clear();
			Array.Clear(UpdatedCellsMask, 0, UpdatedCellsMask.Length);
			Renderer.EmitSignal(ChunkRendererCS.SignalName.CellsUpdated);

		}


		// a function to put all the necessary variables to carry over when moving cells
		public void SwapCells(Cell copyCell, Cell pasteCell)
		{
			Elements PasteCellElement = pasteCell.Element;
			Dictionary<string, int> PasteCellTypeAttributes = pasteCell.TypeAttributes;

			pasteCell.Element = copyCell.Element;
			pasteCell.TypeAttributes = copyCell.TypeAttributes;

			copyCell.Element = PasteCellElement;
			copyCell.TypeAttributes = PasteCellTypeAttributes;
		}


		public void Sleep()
		{
			Sleeping = true;
			Renderer.Sleeping = true;
		}

		public void Wake()
		{
			Sleeping = false;
			Renderer.Sleeping = false;
		}


	}
}
