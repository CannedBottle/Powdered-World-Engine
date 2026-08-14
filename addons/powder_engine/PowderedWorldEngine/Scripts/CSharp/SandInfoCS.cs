using Godot;
using System;
using System.Collections.Generic;
using static Elements;
using static ElementAttributes;


[Tool]
public partial class SandInfoCS : Node
{
	// ---------------------------- Element Attribute Storage ---------------------------- //

	public static readonly string ElementResourcePath = "res://addons/powder_engine/PowderedWorldEngine/ElementData.tres";

	// the instance of the ElementStorage resource, which contains all element data.
	public static readonly ElementStorage ElementResource = ResourceLoader.Load<ElementStorage>(ElementResourcePath);

	
	public static ElementStorage GetElementResource()
	{
		return ElementResource;
	}

	public static void SaveElementStorage()
	{
		ElementResource.EmitSignal(ElementStorage.SignalName.ElementsSaved);

		Error error = ResourceSaver.Save(ElementResource, ElementResourcePath, ResourceSaver.SaverFlags.None);
		if(error == Error.Ok)
		{
			GD.Print("Successfully saved elements to " + ElementResourcePath);
		}
		else
		{
			GD.PrintErr("Failed to save elements. Error: " + error);
		}
	}

	// ----------------------------------------------------------------------------------------------------------------------------------- //

	public static string PluginVersion = "1.0";


	// a way of determining what special attributes an element has.
	public enum ElementTypes
	{
		STATIC,
		SOLID,
		LIQUID,
		GAS
	}

	public static Godot.Collections.Array<StringName> GetElementTypes()
	{
		Godot.Collections.Array<StringName> TypeList = new Godot.Collections.Array<StringName>{};

		foreach(string name in ElementTypes.GetNames<ElementTypes>())
		{
			TypeList.Add((StringName)name);
		}

		return TypeList;
	}

	// a way to add small features to an element and combine them, to allow for more element combinations.
	public enum ElementFlags
	{
		FLAMMABLE,
		ACID_RESISTANT,
		INDESTRUCTIBLE,

	}

	public static Godot.Collections.Array<StringName> GetElementFlags()
	{
		Godot.Collections.Array<StringName> TypeList = new Godot.Collections.Array<StringName>{};

		foreach(string name in ElementFlags.GetNames<ElementFlags>())
		{
			TypeList.Add((StringName)name);
		}

		return TypeList;
	}

	public static Godot.Collections.Array<String> GetElementFlagsAsString()
	{
		Godot.Collections.Array<String> TypeList = new Godot.Collections.Array<String>{};

		foreach(string name in ElementFlags.GetNames<ElementFlags>())
		{
			TypeList.Add((String)name);
		}

		return TypeList;
	}

	// -------------------------------------------------------------------------------------
	
	/// <summary>
	/// Names to make the neighbor positions more readable.
	/// </summary>
	public enum Neighbors
	{
		TOPLEFT,
		TOPMIDDLE,
		TOPRIGHT,
		LEFTMIDDLE,
		RIGHTMIDDLE,
		BOTTOMLEFT,
		BOTTOMMIDDLE,
		BOTTOMRIGHT
	}


	// ------------------------------------ USEFUL FUNCTIONS ----------------------------- //
	public static int CellPosToChunkPos(Vector2I cellPos, int chunkSize)
	{
		Vector2I ChunkPos = new Vector2I((int)Math.Floor((decimal)cellPos.X / (decimal)chunkSize),
		(int)Math.Floor((decimal)cellPos.Y / (decimal)chunkSize));
		
		int LocalX = cellPos.X - (ChunkPos.X * chunkSize);
		int LocalY = cellPos.Y - (ChunkPos.Y * chunkSize);
		return LocalY * chunkSize + LocalX;
	}

	public static void UpdateCloseChunks(Cell cell, PowderSimulation simRef)
	{
		// if on edge of chunk and successfully updates, wake chunk next to it
		if(cell.ChunkEdge)
		{
			//wake chunk nearest to cell   (no bool to int now i have to use ternary ops :[ )
			Vector2I PosAddition = new Vector2I(
				(cell.Position.X == cell.CellChunk.MaxExtents.X ? 1 : 0) - (cell.Position.X == cell.CellChunk.MinExtents.X ? 1 : 0),
				(cell.Position.Y == cell.CellChunk.MaxExtents.Y ? 1 : 0) - (cell.Position.Y == cell.CellChunk.MinExtents.Y ? 1 : 0)
			);
			
			if(PosAddition != Vector2.Zero && simRef.SimContainsChunk(cell.CellChunk.ChunkPosition + PosAddition))
			{
				Chunk NeighborChunk = simRef.GetChunk(cell.CellChunk.ChunkPosition + PosAddition);

				NeighborChunk.Wake();
				//Inflate DirtyRect across chunk borders, fixes liquids not falling when dirtyrect is stuck on the other side of the chunk
				NeighborChunk.MarkCellUpdated(NeighborChunk.Cells[CellPosToChunkPos(cell.Position + PosAddition, cell.ChunkSize)], false);

			}

		}
	}

	// a function to put all the necessary variables to carry over when moving cells
	public static void SwapCells(Cell copyCell, Cell pasteCell)
	{
		AllElements PasteCellElement = pasteCell.Element;
		byte[] PasteCellFields = pasteCell.Fields;

		pasteCell.Element = copyCell.Element;
		pasteCell.Fields = copyCell.Fields;

		copyCell.Element = PasteCellElement;
		copyCell.Fields = PasteCellFields;

		copyCell.CellChunk.MarkCellUpdated(copyCell);
		pasteCell.CellChunk.MarkCellUpdated(pasteCell);
	}

	// --------------------------------- ELEMENT MOVEMENT RULESETS ----------------------- //

	// First run Reactions, then run Movement
	public static void OnUpdate(Cell cell, PowderSimulation simRef)
	{
		// Reactions *********************************************************************
		// TODO: Change this section when implementing custom reaction rulesets (and movement section when making custom movements)
		switch (cell.Element)
		{
			case AllElements.ACID:
				Cell neighbor = cell.SearchNeighborElements(simRef, AllElements.AIR, true, AllElements.ACID);
				if(neighbor != null && neighbor.Element != AllElements.ACID && !neighbor.Attributes.Flags.Contains(ElementFlags.ACID_RESISTANT))
				{
					cell.Element = AllElements.AIR;
					cell.CellChunk.MarkCellUpdated(cell);
					neighbor.Element = AllElements.AIR;
					neighbor.CellChunk.MarkCellUpdated(neighbor);
					return;
				}

				break;
		}

		// Movement ******************************************************************************--

		switch (cell.Attributes.MovementType)
		{
			case MoveTypes.SAND: // ---------------------------------------

				if(cell.TryMove(Neighbors.BOTTOMMIDDLE, simRef) == false)
				{
					if(cell.Fields[cell.GetFieldIndex("R#random")] == 0)
					{
						if(cell.TryMove(Neighbors.BOTTOMRIGHT, simRef) == false)
						{
							cell.TryMove(Neighbors.BOTTOMLEFT, simRef);
						}
					}
					else
					{
						if(cell.TryMove(Neighbors.BOTTOMLEFT, simRef) == false)
						{
							cell.TryMove(Neighbors.BOTTOMRIGHT, simRef);
						}
					}
				}
				break;
			
			case MoveTypes.LIQUID: // ---------------------------------------

				if(cell.TryMove(Neighbors.BOTTOMMIDDLE, simRef) == false)
				{
					bool success;
					if(cell.Fields[cell.GetFieldIndex("R#random")] == 0)
					{
						success = cell.TryMove(Neighbors.BOTTOMRIGHT, simRef);
						if(success == false)
						{
							success = cell.TryMove(Neighbors.BOTTOMLEFT, simRef);
						}
					}
					else
					{
						success = cell.TryMove(Neighbors.BOTTOMLEFT, simRef);
						if(success == false)
						{
							success = cell.TryMove(Neighbors.BOTTOMRIGHT, simRef);
						}
					}

					if(success == false)
					{
						byte[] tempAtts = cell.Fields;

						if(cell.Fields[cell.GetFieldIndex("R#direction")] == 1)
						{
							success = cell.TryMove(Neighbors.RIGHTMIDDLE, simRef);
							tempAtts[cell.GetFieldIndex("bumps")] += success ? (byte)0 : (byte)1;
							tempAtts[cell.GetFieldIndex("R#direction")] = tempAtts[cell.GetFieldIndex("bumps")] >= 3 ? (byte)0 : (byte)1;
							tempAtts[cell.GetFieldIndex("bumps")] = tempAtts[cell.GetFieldIndex("R#direction")] == 1 ? tempAtts[cell.GetFieldIndex("bumps")] : (byte)0;

							if(success == false)
							{
								cell.TryMove(Neighbors.LEFTMIDDLE, simRef);
							}
						}
						else
						{
							success = cell.TryMove(Neighbors.LEFTMIDDLE, simRef);
							tempAtts[cell.GetFieldIndex("bumps")] += success ? (byte)0 : (byte)1;
							tempAtts[cell.GetFieldIndex("R#direction")] = tempAtts[cell.GetFieldIndex("bumps")] >= 3 ? (byte)1 : (byte)0;
							tempAtts[cell.GetFieldIndex("bumps")] = tempAtts[cell.GetFieldIndex("R#direction")] == 0 ? tempAtts[cell.GetFieldIndex("bumps")] : (byte)0;

							if(success == false)
							{
								cell.TryMove(Neighbors.RIGHTMIDDLE, simRef);
							}
						}
					}
				}
				

				break;

			case MoveTypes.STONE: // ----------------------------------------

				cell.TryMove(Neighbors.BOTTOMMIDDLE, simRef);
				break;
		}

	}




	// --------------------------------- CELL / CHUNK CLASSES ----------------------------- //

	public class Cell
	{
		// --------------- STATICS

		private static Dictionary<Neighbors, Vector2I> NeighborOffsets = new Dictionary<Neighbors, Vector2I>
		{
			{SandInfoCS.Neighbors.TOPLEFT, new Vector2I(-1, -1)},
			{SandInfoCS.Neighbors.TOPMIDDLE, new Vector2I(0, -1)},
			{SandInfoCS.Neighbors.TOPRIGHT, new Vector2I(1, -1)},
			{SandInfoCS.Neighbors.LEFTMIDDLE, new Vector2I(-1, 0)},
			{SandInfoCS.Neighbors.RIGHTMIDDLE, new Vector2I(1, 0)},
			{SandInfoCS.Neighbors.BOTTOMLEFT, new Vector2I(-1, 1)},
			{SandInfoCS.Neighbors.BOTTOMMIDDLE, new Vector2I(0, 1)},
			{SandInfoCS.Neighbors.BOTTOMRIGHT, new Vector2I(1, 1)},
		};

		// ------------------------------------

		public int ChunkSize;

		public Vector2I Position = new Vector2I();
	
		private AllElements _element;
		public AllElements Element
		{
			get => _element;
			set
			{
				_element = value;

				Attributes = ElementResource.AllElementAttributes[(StringName)Element.ToString()];


				Brightness = (float)GD.RandRange(1.0 - Attributes.NoiseStrength, 1.0);

			}
		}

		public ElementAttributes Attributes;

		public byte[] Fields = new byte[8];

		public AllElements[] NeighborElements = new AllElements[8];

		public float Brightness;
		public Chunk CellChunk;

		// idx of cell inside the chunk's own cells list, basically local position
		public int ChunkIdx;

		// if on the edge of a chunk, so can decide whether to update a chunk beside it / cross over to another chunk
		public bool ChunkEdge = false;


		public Cell(Vector2I cellPosition, int chunkSize, AllElements cellElement, Chunk cellChunk, int cellChunkIdx)
		{
			Position = cellPosition;
			ChunkSize = chunkSize;
			Element = cellElement;
			CellChunk = cellChunk;
			ChunkIdx = cellChunkIdx;


			ChunkEdge = Position.X == CellChunk.MaxExtents.X || Position.Y == CellChunk.MaxExtents.Y || Position.X == CellChunk.MinExtents.X || Position.Y == CellChunk.MinExtents.Y;

		}


		public void ReplaceTypeAttributesWithDefault()
		{
			Attributes.GetDefaultFieldArray().CopyTo(Fields, 0);
		}

		public void FindNeighborElements(PowderSimulation simRef)
		{
			Array.Clear(NeighborElements);

			int i = 0;
			foreach(Neighbors neighbor in Enum.GetValues<Neighbors>())
			{
					
				#nullable enable
				Cell? NeighborCell = GetNeighbor(neighbor, simRef);
				#nullable disable

				if(NeighborCell == null)
				{
					continue;
				}

				NeighborElements[i] = NeighborCell.Element;
				
				i++;
			}
		}

		#nullable enable
		public Cell? SearchNeighborElements(PowderSimulation simRef, AllElements targetElement, bool opposite = false, AllElements? SecondaryTarget = null)
		{
			foreach(Neighbors neighbor in Enum.GetValues<Neighbors>())
			{

				Cell? NeighborCell = GetNeighbor(neighbor, simRef);

				if(NeighborCell == null)
				{
					return null;
				}

				if(!opposite)
				{
					if (NeighborCell.Element == targetElement || NeighborCell.Element == SecondaryTarget)
					{
						return NeighborCell;
					}
				}
				else
				{
					if (NeighborCell.Element != targetElement && SecondaryTarget != null && NeighborCell.Element != SecondaryTarget)
					{
						return NeighborCell;
					}
				}
				
			}
			return null;
		}


		public Cell? GetNeighbor(Neighbors neighbor, PowderSimulation simRef)
		{
			Vector2I NeighborPos = Position + NeighborOffsets[neighbor];
			
			if(!simRef.SimContainsCell(NeighborPos))
			{
				return null;
			}

			Cell NeighborCell;

			
			if (CellChunk.Contains(NeighborPos))
			{
				NeighborCell = CellChunk.Cells[ChunkIdx + simRef.NeighborIndexOffsets[neighbor]];
			}
			else
			{
				NeighborCell = simRef.GetCell(NeighborPos);
			}
			
			return NeighborCell;
		}

		#nullable disable

		public int GetFieldIndex(string field)
		{
			return Attributes.FieldGetIndex(field);
		}

		// ---------------------------------======= Movement Stuff =======--------------------------------------- //

		/// <summary>
		/// </summary>
		/// <param name="NeighborCell"></param>
		/// <returns>whether the cell can move or not, <b>only</b> comparing the <c>Cell.Attributes.Type</c> of both cells.</returns>
		public bool CanMove(Cell NeighborCell)
		{
			bool ValidMove = false;

			switch (Attributes.Type)
			{
				case ElementTypes.SOLID:
					ValidMove = NeighborCell.Attributes.Type == ElementTypes.LIQUID || NeighborCell.Attributes.Type == ElementTypes.GAS;
					break;
				case ElementTypes.LIQUID:
					ValidMove = NeighborCell.Attributes.Type == ElementTypes.GAS;
					break;
			}

			return ValidMove;
		}

		///<summary>
		///returns whether the move that successful or not.
		/// </summary>
		public bool TryMove(Neighbors ToNeighbor, PowderSimulation simRef)
		{
			
			#nullable enable
			Cell? NeighborCell = GetNeighbor(ToNeighbor, simRef);
			#nullable disable

			if(NeighborCell == null)
			{
				return false;
			}

			if (NeighborCell.CellChunk.UpdatedCellsMask[NeighborCell.ChunkIdx])
			{
				return false;
			}

			//valid cell to move checking (per type check)
			bool ValidMove;

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
				

				simRef.QueueSwap(this, NeighborCell);
				NeighborCell.CellChunk.Wake();

				CellChunk.MarkCellUpdated(this);
				NeighborCell.CellChunk.MarkCellUpdated(NeighborCell);
				
				if(CellChunk == NeighborCell.CellChunk)
				{
					UpdateCloseChunks(this, simRef);
				}
				else
				{
					UpdateCloseChunks(NeighborCell, simRef);
				}

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

		//how many updates of no change before sleeping
		public int Insomnia = 1;
		//keeps track of updates of no change for insomnia
		public int InsomniaCount = 0;
		public Cell[] Cells;
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

		
		public bool Sleeping = false;

		public ChunkRendererCS Renderer;

		public Chunk(int chunkSize, Vector2I chunkPosition, int insomnia)
		{
			ChunkSize = chunkSize;
			ChunkPosition = chunkPosition;
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
			Cells = new Cell[ChunkSize*ChunkSize];
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

					Cells[Idx] = new Cell(Pos, ChunkSize, AllElements.AIR, this, Idx);

					Idx += 1;
				}
			}

		}

		/// <summary>
		/// Updates a single cell in the chunk.
		/// </summary>
		/// <param name="cell"></param>
		/// <param name="simRef"></param>
		public bool UpdateCell(Cell cell, PowderSimulation simRef)
		{
			if (Sleeping)
			{
				return false;
			}


			if (HasValidDirtyRect)
			{

				Vector2I paddedMax = DirtyRectMax + new Vector2I(2, 2);
				Vector2I paddedMin = DirtyRectMin - new Vector2I(2, 2);
		
				if(cell.Position.X > paddedMax.X || cell.Position.Y > paddedMax.Y
				|| cell.Position.X < paddedMin.X || cell.Position.Y < paddedMin.Y)
				{
					return false;
				}
				
					
			}

			if(UpdatedCellsMask[cell.ChunkIdx] == true || cell.Element == AllElements.AIR)
			{
				return false;
			}

			OnUpdate(cell, simRef);
			return true;

			
		}

		/// <summary>
		/// Updates every cell in the chunk. if <c>tick</c> is 1, it loops from front to back of the cells list. If <c>tick</c> is 0, it does the opposite.
		/// </summary>
		/// <param name="simRef"></param>
		/// <param name="tick"></param>
		public void UpdateCells(PowderSimulation simRef, int tick)
		{
			if (Sleeping)
			{
				return;
			}


			if (tick == 1)
			{
				for(int i = 0; i < Cells.Length; i++)
				{
					Cell cell = Cells[i];

					UpdateCell(cell, simRef);

				}
					
			}
			else
			{
				for(int i = Cells.Length - 1; i >= 0; i--)
				{
					Cell cell = Cells[i];

					UpdateCell(cell, simRef);

				}
			}


			DecideSleepState();

		}

		/// <summary>
		/// Sleeps chunk if no cells were updated, taking <c>Insomnia</c> into account. <b>Should be called AFTER updating the chunk.</b>
		/// </summary>
		public void DecideSleepState()
		{
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

		/// <summary>
		/// <c>blockUpdates</c> decides whether to mark the cell as updated in <c>UpdatedCellsMask</c> as it does with <c>UpdatedCellsIndexes</c>.
		/// </summary>
		/// <param name="cell"></param>
		/// <param name="blockUpdates"></param>
		public void MarkCellUpdated(Cell cell, bool blockUpdates = true)
		{
			Wake();

			if (UpdatedCellsMask[cell.ChunkIdx] == true)
			{
				return;
			}

			UpdatedCellsIndexes.Add(cell.ChunkIdx);
			if(blockUpdates)
			{
				UpdatedCellsMask[cell.ChunkIdx] = true;
			}
		}


		public void UpdateDirtyRect()
		{
			HasValidDirtyRect = UpdatedCellsIndexes.Count > 0;
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
				Color Col = cell.Attributes.BaseColor;
				//darkness of pixel
				float D = 1.0f - cell.Brightness;
				//darkness applied to only non-alpha channels
				Col.R -= D;
				Col.G -= D;
				Col.B -= D;
				// sets the pixel on the renderer's Image to the correct color
				Renderer.ChunkImage.SetPixel(idx % ChunkSize, (int)Mathf.Floor(idx / ChunkSize), Col);
			}
			Renderer.ChunkTexture.Update(Renderer.ChunkImage);

			UpdatedCellsIndexes.Clear();
			Array.Clear(UpdatedCellsMask, 0, UpdatedCellsMask.Length);

		}


		// ************* Helpers ------------------

		public void ResetDirtyRect()
		{
			DirtyRectMax = MinExtents;
			DirtyRectMin = MaxExtents;
		}

		/// <summary>
		/// </summary>
		/// <param name="pos"></param>
		/// <returns>If the given position refers to a cell inside this chunk.</returns>
		public bool Contains(Vector2I pos)
		{
			return pos.X <= MaxExtents.X && pos.Y <= MaxExtents.Y 
			&& pos.X >= MinExtents.X && pos.Y >= MinExtents.Y;
		}

		public void Sleep()
		{
			Sleeping = true;
		}

		public void Wake()
		{
			Sleeping = false;
		}


		public void ReplaceAll(AllElements with)
		{
			foreach(Cell cell in Cells)
			{
				cell.Element = with;
			}
		}

		public void ClearAll()
		{
			foreach(Cell cell in Cells)
			{
				cell.Element = AllElements.AIR;
			}
		}

	}
}
