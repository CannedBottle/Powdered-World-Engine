using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Resolvers;
using static Elements;


[Tool]
public partial class SandInfoCS : Node
{
	// ---------------------------- Element Attribute Storage ---------------------------- //
	public partial class ElementAttributes : RefCounted
	{
		public AllElements Id;

		public string NameString;

		public ElementTypes Type;

		public Godot.Collections.Array<ElementFlags> Flags;

		public Color BaseColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);

		public float NoiseStrength;

		#nullable enable
		public ElementAttributes(AllElements EId, ElementTypes EType, Color EColor, float ENoiseStrength, Godot.Collections.Array<ElementFlags>? EFlags)
		{
			
			Id = EId;
			Type = EType;
			BaseColor = EColor;
			NoiseStrength = ENoiseStrength;
			if(EFlags != null)
			{
				Flags = EFlags;		
			}

		}
		#nullable disable

	}


	// the instance of the ElementStorage resource, which contains all element data.
	public static readonly ElementStorage ElementResource = GD.Load<ElementStorage>("res://addons/powder_engine/PowderedWorldEngine/ElementData.tres");

	
	public static ElementStorage GetElementResource()
	{
		return ElementResource;
	}

	// ----------------------------------------------------------------------------------------------------------------------------------- //


	// a way of determining what special attributes an element has.
	public enum ElementTypes
	{
		STATIC,
		SOLID,
		LIQUID,
		GAS
	}

	// a way to add small features to an element and combine them, to allow for more element combinations.
	public enum ElementFlags
	{
		FLAMMABLE,

	}


	public static Godot.Collections.Array<String> ElementNames = new Godot.Collections.Array<String>
	{
		"AIR",
		"SAND",
		"WATER",
		"ACID",
		"STONE",
		"WALL",
	};

	

	public static readonly Dictionary<AllElements, ElementTypes> ElementToType = new Dictionary<AllElements, ElementTypes>
	{
		{AllElements.AIR, ElementTypes.STATIC},
		{AllElements.SAND, ElementTypes.SOLID},
		{AllElements.WATER, ElementTypes.LIQUID},
		{AllElements.ACID, ElementTypes.LIQUID},
		{AllElements.STONE, ElementTypes.SOLID},
		{AllElements.WALL, ElementTypes.STATIC}
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


	public static readonly Dictionary<AllElements, Vector4> BaseElementColors = new Dictionary<AllElements, Vector4>
	{
		{AllElements.AIR, Vector4.Zero},
		{AllElements.SAND, new Vector4(1.0f, 0.93f, 0.474f, 1.0f)},
		{AllElements.WATER, new Vector4(0.112f, 0.644f, 0.93f, 0.6f)},
		{AllElements.ACID, new Vector4(0.678f, 1.0f, 0.31f, 0.75f)},
		{AllElements.STONE, new Vector4(0.58f, 0.58f, 0.58f, 1.0f)},
		{AllElements.WALL, new Vector4(0.27f, 0.27f, 0.27f, 1.0f)},
	};

	// --------------------------- Get / Set For Static Element Defaults ----------------- //

	public static Godot.Collections.Array<String> GetElementNames()
	{
		return ElementNames.Duplicate();
	}
	
	public static void AddElementName(string name)
	{
		ElementNames.Append(name);
	}

	public static void RemoveElementName(string name)
	{
		ElementNames.Remove(name);
	}

	public static void ClearElementNames()
	{
		ElementNames.Clear();
	}

	// ------------------------------------ USEFUL FUNCTIONS ----------------------------- //
	public static int WorldPosToChunkPos(Vector2I worldPos, int chunkSize)
	{
		Vector2I ChunkPos = new Vector2I(worldPos.X / chunkSize, worldPos.Y / chunkSize);
		int LocalX = worldPos.X - (ChunkPos.X * chunkSize);
		int LocalY = worldPos.Y - (ChunkPos.Y * chunkSize);
		return LocalY * chunkSize + LocalX;
	}

	public static void UpdateCloseChunks(Cell cell, PowderSimulation simRef)
	{
		// if on edge of chunk and successfully updates, wake chunk next to it
				if(cell.ChunkEdge && !cell.SimEdge)
				{
					//wake chunk nearest to cell   (no bool to int now i have to use ternary ops :[ )
					Vector2I PosAddition = new Vector2I(
						(cell.Position.X == cell.CellChunk.MaxExtents.X ? 1 : 0) - (cell.Position.X == cell.CellChunk.MinExtents.X ? 1 : 0),
						(cell.Position.Y == cell.CellChunk.MaxExtents.Y ? 1 : 0) - (cell.Position.Y == cell.CellChunk.MinExtents.Y ? 1 : 0)
					);

					if(PosAddition != Vector2.Zero)
					{
						Chunk NeighborChunk = simRef.Chunks[cell.CellChunk.ChunkPosition + PosAddition];

						NeighborChunk.Wake();
						//Inflate DirtyRect across chunk borders, fixes liquids not falling when dirtyrect is stuck on the other side of the chunk
						NeighborChunk.MarkCellUpdated(NeighborChunk.Cells[WorldPosToChunkPos(cell.Position + PosAddition, cell.ChunkSize)]);
						 
					}
				}
	}

	// --------------------------------- ELEMENT MOVEMENT RULESETS ----------------------- //

	public static void OnUpdate(Cell cell, PowderSimulation simRef)
	{

		switch (cell.Element)
		{
			case AllElements.SAND:
					if(cell.TryMove("bottommiddle", simRef) == false)
					{
						if(cell.TypeAttributes["random"] == 0)
						{
							if(cell.TryMove("bottomright", simRef) == false)
							{
								cell.TryMove("bottomleft", simRef);
							}
						}
						else
						{
							if(cell.TryMove("bottomleft", simRef) == false)
							{
								cell.TryMove("bottomright", simRef);
							}
						}
					}
					break;
			

			case AllElements.STONE:
				cell.TryMove("bottommiddle", simRef);
				break;	

			case AllElements.ACID:
				Cell neighbor = cell.SearchNeighborElements(simRef, AllElements.AIR, true, AllElements.ACID);
				if(neighbor != null && neighbor.Element != AllElements.ACID)
				{
					cell.Element = AllElements.AIR;
					cell.CellChunk.MarkCellUpdated(cell);
					neighbor.Element = AllElements.AIR;
					neighbor.CellChunk.MarkCellUpdated(neighbor);
					return;
				}

				break;
		}


		if(cell.CellType == ElementTypes.LIQUID)
		{
			if(cell.TryMove("bottommiddle", simRef) == false)
			{
				bool success;
				if(cell.TypeAttributes["random"] == 0)
				{
					success = cell.TryMove("bottomright", simRef);
					if(success == false)
					{
						success = cell.TryMove("bottomleft", simRef);
					}
				}
				else
				{
					success = cell.TryMove("bottomleft", simRef);
					if(success == false)
					{
						success = cell.TryMove("bottomright", simRef);
					}
				}

				if(success == false)
				{
					if(cell.TypeAttributes["direction"] == 1)
					{
						success = cell.TryMove("rightmiddle", simRef);
						cell.TypeAttributes["direction"] = success ? 1 : 0;

						if(success == false)
						{
							cell.TryMove("leftmiddle", simRef);
						}
					}
					else
					{
						success = cell.TryMove("leftmiddle", simRef);
						cell.TypeAttributes["direction"] = success ? 0 : 1;

						if(success == false)
						{
							cell.TryMove("rightmiddle", simRef);
						}
					}
				}
			}
		}
		

	}




	// --------------------------------- CELL / CHUNK CLASSES ----------------------------- //

	public class Cell
	{
		
		public Vector2I Position = new Vector2I();
		public ElementTypes CellType;
		private AllElements _element;
		public AllElements Element
		{
			get => _element;
			set
			{
				_element = value;
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
		public Dictionary<string, Vector2I?> Neighbors = new Dictionary<string, Vector2I?>(); // if neighbor is an edge, will show up as (-1, -1)

		public List<AllElements> NeighborElements = new List<AllElements>();
		public readonly List<string> NeighborStrings = new List<string>([
			"topleft", "topmiddle", "topright", "leftmiddle", "rightmiddle", "bottomleft", "bottommiddle", "bottomright"
		]);

		public float Brightness;
		public int ChunkSize;
		public Chunk CellChunk;
		public Vector2I SimSize = new Vector2I();

		// idx of cell inside the chunk's own cells list, basically local position
		public int ChunkIdx;

		// if on the edge of a chunk, so can decide whether to update a chunk beside it / cross over to another chunk
		public bool ChunkEdge = false;
		public bool SimEdge = false;


		public Cell(Vector2I cellPosition, int chunkSize, AllElements cellElement, Vector2I simulationSize, Chunk cellChunk, int cellChunkIdx)
		{
			Position = cellPosition;
			ChunkSize = chunkSize;
			Element = cellElement;
			CellChunk = cellChunk;
			ChunkIdx = cellChunkIdx;

			SimSize = simulationSize;


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

			for(int y = -1; y <= 1; y++)
			{
				for(int x = -1; x <= 1; x++)
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
						Neighbors[NeighborStrings[LoopNum]] = null;
					}


					LoopNum += 1;
				}
			}
		}


		public void FindNeighborElements(PowderSimulation simRef)
		{
			NeighborElements.Clear();

			foreach(Vector2I pos in Neighbors.Values)
			{

				Chunk ActualChunk = CellChunk;

				if (ChunkEdge)
				{
					Vector2I NewChunkPos = new Vector2I(pos.X / CellChunk.ChunkSize, pos.Y / CellChunk.ChunkSize);

					ActualChunk = simRef.Chunks[NewChunkPos];
				}

				int NeighborIdx = WorldPosToChunkPos(pos, ChunkSize);

				NeighborElements.Add(ActualChunk.Cells[NeighborIdx].Element);
				
			}
		}

		#nullable enable
		public Cell? SearchNeighborElements(PowderSimulation simRef, AllElements targetElement, bool opposite = false, AllElements? SecondaryTarget = null)
		{
			foreach(Vector2I? pos in Neighbors.Values)
			{
				Vector2I neighborPos;
				if(pos == null)
				{
					continue;
				}
				else
				{
					neighborPos = pos.Value;
				}
				Chunk ActualChunk = CellChunk;

				if (ChunkEdge)
				{
					Vector2I NewChunkPos = new Vector2I(neighborPos.X / CellChunk.ChunkSize, neighborPos.Y / CellChunk.ChunkSize);

					ActualChunk = simRef.Chunks[NewChunkPos];
				}
				
				int NeighborIdx = WorldPosToChunkPos(neighborPos, ChunkSize);

				if(!opposite)
				{
					if (ActualChunk.Cells[NeighborIdx].Element == targetElement || ActualChunk.Cells[NeighborIdx].Element == SecondaryTarget)
					{
						return ActualChunk.Cells[NeighborIdx];
					}
				}
				else
				{
					if (ActualChunk.Cells[NeighborIdx].Element != targetElement && SecondaryTarget != null && ActualChunk.Cells[NeighborIdx].Element != SecondaryTarget)
					{
						return ActualChunk.Cells[NeighborIdx];
					}
				}
				
			}
			return null;
		}
		#nullable disable

		// ---------------------------------======= Movement Stuff =======--------------------------------------- //
		public bool CanMove(Cell NeighborCell)
		{
			bool ValidMove = false;

			switch (CellType)
			{
				case ElementTypes.SOLID:
					ValidMove = NeighborCell.CellType == ElementTypes.LIQUID || NeighborCell.CellType == ElementTypes.GAS;
					break;
				case ElementTypes.LIQUID:
					ValidMove = NeighborCell.CellType == ElementTypes.GAS;
					break;
			}

			return ValidMove;
		}

		///<summary>
		///returns whether the move that successful or not.
		/// <summary>
		public bool TryMove(string ToNeighbor, PowderSimulation simRef)
		{
			Vector2I? NeighborExist = Neighbors[ToNeighbor];
			Vector2I NeighborPos;
			if(NeighborExist == null)
			{
				return false;
			}
			else
			{
				NeighborPos = NeighborExist.Value;
			}

			Chunk WriteChunk = CellChunk;

			if (ChunkEdge)
			{
				Vector2I NewChunkPos = new Vector2I(NeighborPos.X / CellChunk.ChunkSize, NeighborPos.Y / CellChunk.ChunkSize);

				WriteChunk = simRef.Chunks[NewChunkPos];
			}

			int NeighborIdx = WorldPosToChunkPos(NeighborPos, ChunkSize);

			//valid cell to move checking (per type check)
			bool ValidMove;
			Cell NeighborCell = WriteChunk.Cells[NeighborIdx];
			if(NeighborCell.Element == AllElements.AIR)
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


				UpdateCloseChunks(this, simRef);
				

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
		public PowderSimulation SimRef;

		//how many updates of no change before sleeping
		public int Insomnia = 1;
		//keeps track of updates of no change for insomnia
		public int InsomniaCount = 0;
		public List<Cell> Cells = new List<Cell>();
		//cells to draw, also cells that have been updated in the past frame
		public bool[] UpdatedCellsMask;
		public List<int> UpdatedCellsIndexes = new List<int>();

		//position of chunk in the grid of chunks (not cells)
		public Vector2I ChunkPosition;

		// the max/min values the chunk cells dict contains, used for moving cells from one chunk to another
		public Vector2I MaxExtents;
		public Vector2I MinExtents;


		// ----- dirty rect vars ----- //
		public Vector2I DirtyRectMax;
		public Vector2I DirtyRectMin;
		public bool HasValidDirtyRect;

		

		public Vector2I SimSize;
		public bool Sleeping = false;

		public ChunkRendererCS Renderer;

		public Chunk(int chunkSize, Vector2I chunkPosition, Vector2I simulationSize, int insomnia)
		{
			ChunkSize = chunkSize;
			ChunkPosition = chunkPosition;
			SimSize = simulationSize;
			Insomnia = insomnia;

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

					Cells.Add(new Cell(Pos, ChunkSize, AllElements.AIR, SimSize, this, Idx));

					Idx += 1;
				}
			}

			foreach (Cell cell in Cells)
			{
				cell.FindNeighborIndices();
			}

		}


		public void UpdateCells(PowderSimulation simRef, int tick)
		{

			if (tick == 1)
			{
				for(int i = 0; i < Cells.Count; i++)
				{
					Cell cell = Cells[i];

					if (HasValidDirtyRect)
					{
						if(!(cell.Position <= DirtyRectMax + new Vector2I(2, 2) && cell.Position >= DirtyRectMin - new Vector2I(2, 2)))
						{
							continue;
						}
					}

					if(UpdatedCellsMask[cell.ChunkIdx] == true || cell.Element == AllElements.AIR)
					{
						continue;
					}

					OnUpdate(cell, simRef);

				}
			}
			else
			{
				for(int i = Cells.Count - 1; i > -1; i--)
				{
					Cell cell = Cells[i];

					if (HasValidDirtyRect)
					{
						if(!(cell.Position <= DirtyRectMax + new Vector2I(2, 2) && cell.Position >= DirtyRectMin - new Vector2I(2, 2)))
						{
							continue;
						}
					}

					if(UpdatedCellsMask[cell.ChunkIdx] == true || cell.Element == AllElements.AIR)
					{
						continue;
					}

					OnUpdate(cell, simRef);

				}
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
				Renderer.ChunkImage.SetPixel(idx % ChunkSize, (int)Mathf.Floor(idx / ChunkSize), new Color(Col.X - D, Col.Y - D, Col.Z - D, Col.W));
			}
			Renderer.ChunkTexture.Update(Renderer.ChunkImage);

			UpdatedCellsIndexes.Clear();
			Array.Clear(UpdatedCellsMask, 0, UpdatedCellsMask.Length);

		}


		// a function to put all the necessary variables to carry over when moving cells
		public void SwapCells(Cell copyCell, Cell pasteCell)
		{
			AllElements PasteCellElement = pasteCell.Element;
			Dictionary<string, int> PasteCellTypeAttributes = pasteCell.TypeAttributes;

			pasteCell.Element = copyCell.Element;
			pasteCell.TypeAttributes = copyCell.TypeAttributes;

			copyCell.Element = PasteCellElement;
			copyCell.TypeAttributes = PasteCellTypeAttributes;
		}


		public void Sleep()
		{
			Sleeping = true;
		}

		public void Wake()
		{
			Sleeping = false;
		}


	}
}
