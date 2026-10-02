using Godot;
using System;
using System.Collections.Generic;
using static Elements;
using static ElementAttributes;


[Tool]
public partial class SandInfo : Node
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

	/// <summary>
	/// Updates the default fields in each ElementAttributes instance with the one defined in the custom ElementBehavior class attached to it, if there is any.
	/// </summary>
	public static void UpdateAttributeFields()
	{
		foreach(ElementAttributes attributes in ElementResource.AllElementAttributes.Values)
		{
			attributes.UpdateDefaultFieldsToCustom();
		}
	}

	// ----------------------------------------------------------------------------------------------------------------------------------- //

	/// <summary>
	/// this property is set in the plugin config file, where the plugin is initialized.
	/// </summary>
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
	public static readonly Vector2I TOPLEFT = new Vector2I(-1, -1);
	public static readonly Vector2I TOPMIDDLE = new Vector2I(0, -1);
	public static readonly Vector2I TOPRIGHT = new Vector2I(1, -1);
	public static readonly Vector2I LEFTMIDDLE = new Vector2I(-1, 0);
	public static readonly Vector2I RIGHTMIDDLE = new Vector2I(1, 0);
	public static readonly Vector2I BOTTOMLEFT = new Vector2I(-1, 1);
	public static readonly Vector2I BOTTOMMIDDLE = new Vector2I(0, 1);
	public static readonly Vector2I BOTTOMRIGHT = new Vector2I(1, 1);

	private static Vector2I[] FireSpawnNeighborOrder = [TOPMIDDLE, BOTTOMMIDDLE, LEFTMIDDLE, RIGHTMIDDLE];

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
		// Do not move if it was already updated
		if(cell.CellChunk.IsCellMarked(cell)){return;}

		// Reactions *********************************************************************

		if (RunReactions(cell, simRef))
		{
			return;
		}

		
		// Special Flag Thingies ***************************************************************
		if (cell.Attributes.Flags.Contains(ElementFlags.FLAMMABLE))
		{
			RunFlammable(cell, simRef);

			if(cell.Element == AllElements.FIRE) // make sure it can ignite the ones next to it
			{
				return;
			}
		}

		// Movement ******************************************************************************--

		switch (cell.Attributes.MovementType)
		{
			case MoveTypes.SAND: // ---------------------------------------

				RunSand(cell, simRef);
				break;
			
			case MoveTypes.LIQUID: // ---------------------------------------

				RunLiquid(cell, simRef);
				break;

			case MoveTypes.STONE: // ----------------------------------------
				
				RunStone(cell, simRef);
				break;

			case MoveTypes.GAS: // ------------------------------------------ (upside down water)

				RunGas(cell, simRef);
				break;
			
			case MoveTypes.CUSTOM: // --------------------------------------- (defined in custom behavior)

				if(cell.Attributes.CustomBehavior == null)
				{
					GD.PushWarning("No custom behavior set. Cell will not update.");
					
					break;
				}

				cell.Attributes.CustomBehavior._OnCellUpdate(cell, simRef);

				break;

		}

	}

	/// <summary>
	/// goes through and does all the reactions connected to the cell's element.
	/// </summary>
	/// <returns>Whether or not to stop the update function after this is done.</returns>
	private static bool RunReactions(Cell cell, PowderSimulation simRef)
	{

		// loop over reactions and run that corresponding one
		foreach(Reaction reaction in cell.Attributes.Reactions)
		{
			RunReaction(cell, simRef, reaction);
		}

		if(cell.Element == AllElements.AIR)
		{
			return true;
		}
		else
		{
			return false;
		}
	}

	private static void RunReaction(Cell cell, PowderSimulation simRef, Reaction reaction)
	{
		int currentAmount = 0;
		// fall back on itself if there are no marked neighbors assigned
		Cell LastIteratedCell = cell;
		foreach(Vector2I neighborPos in reaction.MarkedNeighbors)
		{
			Cell tempNullTest = cell.GetNeighbor(neighborPos, simRef);
			// only proceed if the neighbor exists
			if(tempNullTest == null){continue;}

			LastIteratedCell = tempNullTest;

			if(reaction.ElementCheck == null)
			{
				// search for any non-air element
				if(LastIteratedCell.Element != AllElements.AIR && !reaction.ElementExclusions.Contains(LastIteratedCell.Element))
				{
					currentAmount++;
				}
			}
			else
			{
				// search for specified element
				if(LastIteratedCell.Element == reaction.ElementCheck.Value)
				{
					currentAmount++;
				}
			}

			if(CheckReactionAmount(currentAmount, reaction))
			{
				break;
			}
		}

		// only progress if the conditions were met
		if(!CheckReactionAmount(currentAmount, reaction))
		{
			return;	
		}

		// all the different replace types
		switch (reaction.reactionType)
		{
			case Reaction.ReactionTypes.REPLACE_SELF:
				cell.Element = reaction.ReplaceWith;
				cell.CellChunk.MarkCellUpdated(cell);
				break;
			
			case Reaction.ReactionTypes.REPLACE_NEIGHBORS:
				// replace all neighbors
				foreach(Vector2I neighborPos in Cell.Neighbors)
				{

					Cell neighborCell = cell.GetNeighbor(neighborPos, simRef);
					// do not replace if cell is indesctructible
					if (neighborCell.Attributes.Flags.Contains(ElementFlags.INDESTRUCTIBLE))
					{
						continue;
					}

					neighborCell.Element = reaction.ReplaceWith;
					neighborCell.CellChunk.MarkCellUpdated(neighborCell);
				}
				break;
			
			case Reaction.ReactionTypes.REPLACE_MARKED_NEIGHBORS:
				// replace only marked neighbors
				foreach(Vector2I neighborPos in reaction.MarkedNeighbors)
				{
					Cell neighborCell = cell.GetNeighbor(neighborPos, simRef);
					// do not replace if cell is indesctructible
					if (neighborCell.Attributes.Flags.Contains(ElementFlags.INDESTRUCTIBLE))
					{
						continue;
					}

					neighborCell.Element = reaction.ReplaceWith;
					neighborCell.CellChunk.MarkCellUpdated(neighborCell);
				}
				break;
			
			case Reaction.ReactionTypes.REPLACE_LAST_ITERATED_NEIGHBOR:
				// replace only the last neighbor iterated on
				// do not replace if cell is indesctructible
				if (LastIteratedCell.Attributes.Flags.Contains(ElementFlags.INDESTRUCTIBLE))
				{
					break;
				}

				LastIteratedCell.Element = reaction.ReplaceWith;
				LastIteratedCell.CellChunk.MarkCellUpdated(LastIteratedCell);
				break;
			
		}

	}

	private static bool CheckReactionAmount(int currentAmount, Reaction reaction)
	{
		switch (reaction.NeighborElementComparison)
		{
			case Reaction.Comparisons.EQUALS:
				return currentAmount == reaction.MatchingNumToCheck;
			
			case Reaction.Comparisons.GREATER_THAN_OR_EQUAL:
				return currentAmount >= reaction.MatchingNumToCheck;

			case Reaction.Comparisons.LESS_THAN:
				return currentAmount < reaction.MatchingNumToCheck;
				
		}

		return false;
	}

	/// <summary>
	/// The Sand movement preset. When using this, make sure to have the same default fields.
	/// </summary>
	/// <param name="cell"></param>
	/// <param name="simRef"></param>
	public static void RunSand(Cell cell, PowderSimulation simRef)
	{
		if(cell.TryMove(BOTTOMMIDDLE, simRef) == false)
				{
					if(cell.GetField("R#random") == 0)
					{
						if(cell.TryMove(BOTTOMRIGHT, simRef) == false)
						{
							cell.TryMove(BOTTOMLEFT, simRef);
						}
					}
					else
					{
						if(cell.TryMove(BOTTOMLEFT, simRef) == false)
						{
							cell.TryMove(BOTTOMRIGHT, simRef);
						}
					}
				}
	}

	/// <summary>
	/// The Liquid movement ruleset. When using this, make sure to have the same default fields.
	/// </summary>
	/// <param name="cell"></param>
	/// <param name="simRef"></param>
	public static void RunLiquid(Cell cell, PowderSimulation simRef)
	{
		if(cell.TryMove(BOTTOMMIDDLE, simRef) == false)
				{
					bool success;
					if(cell.GetField("R#random") == 0)
					{
						success = cell.TryMove(BOTTOMRIGHT, simRef);
						if(success == false)
						{
							success = cell.TryMove(BOTTOMLEFT, simRef);
						}
					}
					else
					{
						success = cell.TryMove(BOTTOMLEFT, simRef);
						if(success == false)
						{
							success = cell.TryMove(BOTTOMRIGHT, simRef);
						}
					}

					if(success == false)
					{
						byte[] tempAtts = cell.Fields;

						if(cell.GetField("R#direction") == 1)
						{
							success = cell.TryMove(RIGHTMIDDLE, simRef);
							tempAtts[cell.GetFieldIndex("bumps")] += success ? (byte)0 : (byte)1;
							tempAtts[cell.GetFieldIndex("R#direction")] = tempAtts[cell.GetFieldIndex("bumps")] >= 3 ? (byte)0 : (byte)1;
							tempAtts[cell.GetFieldIndex("bumps")] = tempAtts[cell.GetFieldIndex("R#direction")] == 1 ? tempAtts[cell.GetFieldIndex("bumps")] : (byte)0;

							if(success == false)
							{
								cell.TryMove(LEFTMIDDLE, simRef);
							}
						}
						else
						{
							success = cell.TryMove(LEFTMIDDLE, simRef);
							tempAtts[cell.GetFieldIndex("bumps")] += success ? (byte)0 : (byte)1;
							tempAtts[cell.GetFieldIndex("R#direction")] = tempAtts[cell.GetFieldIndex("bumps")] >= 3 ? (byte)1 : (byte)0;
							tempAtts[cell.GetFieldIndex("bumps")] = tempAtts[cell.GetFieldIndex("R#direction")] == 0 ? tempAtts[cell.GetFieldIndex("bumps")] : (byte)0;

							if(success == false)
							{
								cell.TryMove(RIGHTMIDDLE, simRef);
							}
						}
					}
				}
	}

	/// <summary>
	/// The Stone movement ruleset.
	/// </summary>
	/// <param name="cell"></param>
	/// <param name="simRef"></param>
	public static void RunStone(Cell cell, PowderSimulation simRef)
	{
		Cell leftCell = cell.GetNeighbor(LEFTMIDDLE, simRef);
		Cell rightCell = cell.GetNeighbor(RIGHTMIDDLE, simRef);

		if (leftCell == null || leftCell.Element == AllElements.AIR || (leftCell.Attributes.Type != ElementTypes.SOLID && leftCell.Attributes.Type != ElementTypes.STATIC)
		|| rightCell == null || rightCell.Element == AllElements.AIR || (rightCell.Attributes.Type != ElementTypes.SOLID && rightCell.Attributes.Type != ElementTypes.STATIC))
		{
			cell.TryMove(BOTTOMMIDDLE, simRef);
		}
	}

	/// <summary>
	/// The Gas movement ruleset. When using this, make sure to have the same default fields.
	/// </summary>
	/// <param name="cell"></param>
	/// <param name="simRef"></param>
	public static void RunGas(Cell cell, PowderSimulation simRef)
	{
		if(cell.TryMove(TOPMIDDLE, simRef) == false)
				{
					bool success;
					if(cell.GetField("R#random") == 0)
					{
						success = cell.TryMove(TOPRIGHT, simRef);
						if(success == false)
						{
							success = cell.TryMove(TOPLEFT, simRef);
						}
					}
					else
					{
						success = cell.TryMove(TOPLEFT, simRef);
						if(success == false)
						{
							success = cell.TryMove(TOPRIGHT, simRef);
						}
					}

					if(success == false)
					{
						byte[] tempAtts = cell.Fields;

						if(cell.GetField("R#direction") == 1)
						{
							success = cell.TryMove(RIGHTMIDDLE, simRef);
							tempAtts[cell.GetFieldIndex("bumps")] += success ? (byte)0 : (byte)1;
							tempAtts[cell.GetFieldIndex("R#direction")] = tempAtts[cell.GetFieldIndex("bumps")] >= 3 ? (byte)0 : (byte)1;
							tempAtts[cell.GetFieldIndex("bumps")] = tempAtts[cell.GetFieldIndex("R#direction")] == 1 ? tempAtts[cell.GetFieldIndex("bumps")] : (byte)0;

							if(success == false)
							{
								cell.TryMove(LEFTMIDDLE, simRef);
							}
						}
						else
						{
							success = cell.TryMove(LEFTMIDDLE, simRef);
							tempAtts[cell.GetFieldIndex("bumps")] += success ? (byte)0 : (byte)1;
							tempAtts[cell.GetFieldIndex("R#direction")] = tempAtts[cell.GetFieldIndex("bumps")] >= 3 ? (byte)1 : (byte)0;
							tempAtts[cell.GetFieldIndex("bumps")] = tempAtts[cell.GetFieldIndex("R#direction")] == 0 ? tempAtts[cell.GetFieldIndex("bumps")] : (byte)0;

							if(success == false)
							{
								cell.TryMove(RIGHTMIDDLE, simRef);
							}
						}
					}
				}
	}

	/// <summary>
	/// The function that handles FLAMMABLE elements. When using this, make sure to have the "burnProgress" default field (auto-added when an element has the flag "FLAMMABLE").
	/// </summary>
	/// <param name="cell"></param>
	/// <param name="simRef"></param>
	public static void RunFlammable(Cell cell, PowderSimulation simRef)
	{
		byte burnProgress = cell.GetField("burnProgress");

		if(burnProgress >= 255) // if fully burnt, replace element with FIRE
		{
			cell.SetElement(AllElements.FIRE);
			cell.CellChunk.MarkCellUpdated(cell);
			return;
		}

		if(burnProgress <= 0) // look for fire and see if it burns
		{
			if(cell.SearchNeighborElements(simRef, AllElements.FIRE) != null) // if fire is next to it
			{
				if(GD.Randf() > cell.Attributes.SpreadResistance) // roll a chance to ignite and if it does, increment burnProgress
				{
					cell.SetField("burnProgress", 1);
					return;
				}
			}
			else
			{
				return;
			}
		}
		else // do stuff for when its already burning
		{
			cell.CellChunk.MarkCellUpdated(cell); // make sure it keeps burning
			
			// spawn fire around it in a specific order
			foreach(Vector2I neighborPos in FireSpawnNeighborOrder)
			{
				#nullable enable
				Cell? neighbor = cell.GetNeighbor(neighborPos, simRef);
				#nullable disable
				if(neighbor != null && neighbor.Element == AllElements.AIR)
				{
					neighbor.SetElement(AllElements.FIRE);
					neighbor.CellChunk.MarkCellUpdated(neighbor);
					neighbor.SetField("R#MovesBeforeExpire", (byte)GD.RandRange(0, 5));
					break;
				}
			}

			// only tick to deletion if element isnt indestructible
			if(cell.Attributes.Flags.Contains(ElementFlags.INDESTRUCTIBLE)){return;}
		
			float dt = simRef.SimActualDt;
			cell.SetField("burnProgress", (byte)Math.Min(255, burnProgress + dt * (255 / cell.Attributes.BurnSpeed)));

		}

	}

	// --------------------------------- CELL / CHUNK CLASSES ----------------------------- //

	public class Cell
	{
		// --------------- STATICS
		public static readonly Vector2I[] Neighbors = [TOPRIGHT, TOPMIDDLE, LEFTMIDDLE, RIGHTMIDDLE, BOTTOMLEFT, BOTTOMMIDDLE, BOTTOMRIGHT];

		/// <summary>
		/// The number of cell-specific fields each cell has. Each cell's fields are a fixed-size C# array, so this is how many there are for every cell.
		/// </summary>
		public static readonly byte FieldCount = 8;

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

		/// <summary>
		/// Sets this cell's Element while marking it updated on this cell's chunk. Marking a cell updated makes sure it displays the updated information on the simulation.
		/// </summary>
		/// <param name="to"></param>
		public void SetElement(AllElements to)
		{
			Element = to;
			ReplaceTypeAttributesWithDefault();
			CellChunk.MarkCellUpdated(this);
		}

		/// <summary>
		/// Sets a specified field to a specified value. Also clamps the value to a range of (0, 255).
		/// </summary>
		/// <param name="FieldName">The name of the field to access. Include prefixes like R#.</param>
		/// <param name="SetTo">The number to set the field to.</param>
		public void SetField(string FieldName, byte SetTo)
		{
			Fields[GetFieldIndex(FieldName)] = Math.Clamp(SetTo, (byte)0, (byte)255);
		}

		/// <summary></summary>
		/// <param name="FieldName">The field whose value to return.</param>
		/// <returns>A specified field's value.</returns>
		public byte GetField(string FieldName)
		{
			return Fields[GetFieldIndex(FieldName)];
		}

		/// <summary>
		/// Reverts all changes to the <c>Fields</c> array.
		/// </summary>
		public void ReplaceTypeAttributesWithDefault()
		{
			Attributes.GetDefaultFieldArray().CopyTo(Fields, 0);
		}

		#nullable enable
		public Cell? SearchNeighborElements(PowderSimulation simRef, AllElements targetElement, bool opposite = false, int targetAmount = 1, AllElements? SecondaryTarget = null)
		{
			int currentAmount = 0;
			foreach(Vector2I neighbor in Neighbors)
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
						currentAmount++;
						if(currentAmount >= targetAmount)
						{
							return NeighborCell;
						}
					}
				}
				else
				{
					if (NeighborCell.Element != targetElement && SecondaryTarget != null && NeighborCell.Element != SecondaryTarget)
					{
						currentAmount++;
						if(currentAmount >= targetAmount)
						{
							return NeighborCell;
						}
					}
				}
				
			}
			return null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="neighbor">The transformation applied to this cell's Position to get the neighbor cell from. Use SandInfo's constants like TOPLEFT or TOPRIGHT for better readability.</param>
		/// <param name="simRef"></param>
		/// <returns>The neighbor cell specified in <c>neighbor</c>, and <c>null</c> if the neighbor doesn't exist (is past the edge of the simulation)</returns>
		public Cell? GetNeighbor(Vector2I neighbor, PowderSimulation simRef)
		{
			Vector2I NeighborPos = Position + neighbor;
			
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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="field"></param>
		/// <returns>The index to a field in <c>Fields</c>.</returns>
		public int GetFieldIndex(string field)
		{
			return Attributes.FieldGetIndex(field);
		}

		// ---------------------------------======= Movement Stuff =======--------------------------------------- //

		/// <summary>
		/// This is called in TryMove() so if you are just moving the cell, you do not need to call this.
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

		/// <summary>
		/// Attempts to move this cell to one of its neighbors.
		/// </summary>
		/// <returns>Whether the move was successful or not.</returns>
		public bool TryMove(Vector2I ToNeighbor, PowderSimulation simRef)
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

		public event Action<Vector2I> OnChange;

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
		
			OnChange.Invoke(ChunkPosition);

		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="cell"></param>
		/// <returns>Whether or not the given cell was marked "updated" by using MarkCellUpdated()</returns>
		public bool IsCellMarked(Cell cell)
		{
			return UpdatedCellsMask[cell.ChunkIdx];
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
