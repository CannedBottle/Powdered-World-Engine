using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static Elements;



/// <summary>
/// A class that handles the file I/O of saving + loading worlds (files with the <c>.pwdr</c> extension).
/// </summary>
public partial class WorldStreamer : Object
{
    
    // STATICS ----------------------------

    /// <summary>
    /// Creates a new WorldStreamer Object and assigns it the specified file.
    /// </summary>
    /// <returns>a new <c>WorldStreamer</c> object if the file is usable; otherwise returns <c>null</c>.</returns>
    public static WorldStreamer Open(string WorldPath, PowderSimulation Simulation)
    {

        if (!IsFileUsable(WorldPath))
		{
			return null;
		}

        return new WorldStreamer(WorldPath, Simulation);
    }


    /// <summary>
    /// Creates a new WorldStreamer object and a <c>.pwdr</c>file with the given <c>FileName</c> assigned to it. <c>WorldDirPath</c> must point to a directory in which the file can be created. Usually starts with <c>user://</c>.
    /// </summary>
    /// <param name="WorldPath"></param>
    /// <param name="FileName"></param>
    /// <returns></returns>
    public static WorldStreamer Create(string WorldDirPath, string FileName, PowderSimulation Simulation)
    {
        return new WorldStreamer(SaveEmpty(FileName, WorldDirPath), Simulation);
    }


    /// <summary></summary>
	/// <returns>Whether the given file correctly contains the key and the correct file extension.</returns>
	public static bool IsFileUsable(string Path)
	{
		if (!FileAccess.FileExists(Path))
		{
			return false;
		}

		using var testFile = FileAccess.Open(Path, FileAccess.ModeFlags.Read);

		int testCount = 0;

		if(Path.GetExtension() == PowderSimulation.DefaultFileExtension)
		{
			testCount += 1;
		}

		if (testFile.GetPascalString().Contains("PWEngine"))
		{
			testCount += 1;
		}

		return testCount == 2;
	}


    /// <summary>
	/// creates an empty save file. <c>Path</c> must point to a directory in which the new file can be added to. <c>Path</c> also must end with <c>/</c>. <b>Do not</b> include the file extension in <c>Name</c>.
	/// </summary>
	/// <param name="Name"></param>
	/// <param name="Path"></param>
    /// <returns>the path to the newly created file.</returns>
	private static string SaveEmpty(string Name, string Path)
	{

        string fullPath = Path + Name + "." + PowderSimulation.DefaultFileExtension;

		DirAccess.MakeDirRecursiveAbsolute(Path);

		using var worldFile = FileAccess.Open(fullPath, FileAccess.ModeFlags.Write);

		worldFile.StorePascalString("PWEngine " + SandInfoCS.PluginVersion);

		// store file position placeholder for offset dict
		worldFile.Store32(0);

		worldFile.Close();
		worldFile.Dispose();

        return fullPath;
	}


    // --------------------------

    public WorldStreamer(string Path, PowderSimulation Simulation)
    {
        Sim = Simulation;

        WorldFile = FileAccess.Open(Path, FileAccess.ModeFlags.ReadWrite);
    }




    private PowderSimulation Sim;

    private FileAccess WorldFile;



    private Dictionary<Vector2I, int> ChunkFileOffsets = new Dictionary<Vector2I, int>{};



    // ******************* Saving + Loading World -----------------------------------------------------

	/// <summary>
	/// Extracts the information from a <c>.pwdr</c> file, which <c>Path</c> must point to, into a <c>storedWorldInfo</c> struct so the information can be used.
	/// </summary>
	/// <param name="Path"></param>
	/// <returns>A <c>storedWorldInfo</c> struct that represents the entire contents of the file specified in <c>Path</c>. Returns <c>null</c> if the file at <c>Path</c> is unusable.</returns>
	private storedWorldInfo? GetFileData()
	{

		// get header
		string header = WorldFile.GetPascalString();

		// generate element lookup
		Dictionary<int, AllElements> IdxToElement = new Dictionary<int, AllElements>{};

		List<string> elementNames = new List<string>();

		for (int i = 0; i < WorldFile.Get16(); i++)
		{
			string element = WorldFile.GetPascalString();
			IdxToElement.Add(i, Elements.GetFromName(element));

			elementNames.Add(element);
		}

		// collect information required to create the storedWorldInfo instance
		byte cellFields = WorldFile.Get8();

		List<storedChunkInfo> chunkData = new List<storedChunkInfo>();
		// loop over chunks
		for (int i = 0; i < WorldFile.Get32(); i++)
		{
			uint xPos = WorldFile.Get32();
			uint yPos = WorldFile.Get32();

			List<storedCellRun> cellRuns = new List<storedCellRun>();
			// get cell runs
			for (int run = 0; run < WorldFile.Get32(); run++)
			{
				uint runLength = WorldFile.Get32();
				ushort elementIndex = WorldFile.Get16();
				byte[] fields = new byte[cellFields];

				// get the cell-specific fields
				for (int field = 0; field < cellFields; field++)
				{
					fields[field] = WorldFile.Get8();
				}

				cellRuns.Add(new storedCellRun((int)runLength, (short)elementIndex, fields));
			}


			// add chunk data instance
			chunkData.Add(new storedChunkInfo((int)xPos, (int)yPos, cellRuns));

		}

		// finally create and return the world info instance

		return new storedWorldInfo(header, elementNames, cellFields, chunkData, IdxToElement);

	}

	/// <summary>
	/// Saves the data within <c>Data</c> to a <c>.pwdr</c> file while overriding the previous contents.
	/// </summary>
	/// <param name="Data"></param>
	/// <param name="Path"></param>
	/// <returns>Whether the operation was successful or not.</returns>
	private bool SaveFromData(storedWorldInfo Data)
	{

		// keep header
		WorldFile.GetPascalString();
		WorldFile.Resize((long)WorldFile.GetPosition());

		// store element num
		WorldFile.Store16((ushort)Data.ElementNames.Count);
		// store elements
		foreach (string element in Data.ElementNames)
		{
			WorldFile.StorePascalString(element);
		}

		// store cell field num
		WorldFile.Store8(Data.CellFieldNum);

		// store chunk num
		WorldFile.Store32((uint)Data.ChunkNum);
		
		// store chunks
		foreach(storedChunkInfo chunk in Data.Chunks)
		{
			// store x/y position
			WorldFile.Store32((uint)chunk.X);
			WorldFile.Store32((uint)chunk.Y);
			// store run num
			WorldFile.Store32((uint)chunk.CellRunNum);

			// store runs
			foreach(storedCellRun run in chunk.CellRuns)
			{
				// store length
				WorldFile.Store32((uint)run.RunLength);

				// store element index
				WorldFile.Store16((ushort)run.ElementIndex);

				// store cell fields
				foreach(byte field in run.CellFields)
				{
					WorldFile.Store8(field);
				}
			}
		}

		return true;
	}


	/// <summary>
	/// Saves the world to disk at the specified <c>Path</c>, only operating on the chunks currently active in the simulation. <c>Path</c> must point
	/// to a file with the <c>.pwdr</c> extension.
	/// </summary>
	/// <param name="Override">Whether or not to override the contents of the file, essentially replacing the contents with the current ones.</param>
	/// <returns>Whether the operation was successful or not.</returns>
	public bool SaveWorld(bool Override)
	{

		if(Override)
		{
			WorldFile.GetPascalString();
			WorldFile.Resize((long)WorldFile.GetPosition());

			// element lookup table --------------
			StoreElementLookup();

			// store the number of cell-specific fields every cell has
			WorldFile.Store8((byte)Sim.GetCell(new Vector2I(0, 0)).Fields.Count());

			// store number of chunks
			WorldFile.Store32((uint)Sim.Chunks.Count);
			
			// store chunks
			foreach(SandInfoCS.Chunk chunk in Sim.Chunks.Values)
			{
				StoreChunk(chunk);
			}
		}
		else // --------------------------------------------------------------
		{

		}

		return true;
	}


	/// <summary>
	/// stores a chunk's worth of data at the pointer of <c>File</c>.
	/// </summary>
	/// <param name="File"></param>
	/// <param name="Chunk"></param>
	private void StoreChunk(SandInfoCS.Chunk Chunk)
	{
		//store chunk position
		WorldFile.Store32((uint)Chunk.ChunkPosition.X);
		WorldFile.Store32((uint)Chunk.ChunkPosition.Y);


		//store this chunk's cells
		CompressAndStoreCells(Chunk.Cells);
	}

	/// <summary>
	/// Uses RLE (Run-length encoding) to compress horizontal runs of the same element and compress them into one instance. Then stores the cell data at the 
	/// pointer position of the <c>File</c>.
	/// </summary>
	/// <param name="File"></param>
	private void CompressAndStoreCells(SandInfoCS.Cell[] Cells)
	{
		List<StringName> elementOrder = SandInfoCS.ElementResource.ElementOrder.ToList();

		List<int> runLengths = new List<int>();
		List<int> elementIDs = new List<int>();
		// stores the index of the first element in each run to use for collecting cell-specific data
		List<int> beginningIdxs = new List<int>();

		int currentRunLength = 0;
		int prevID = -1;
		int idx = 0;
		foreach(SandInfoCS.Cell cell in Cells)
		{
			// get the unique ID of the element name
			int ID = elementOrder.IndexOf(Enum.GetName(cell.Element));

			// if ID is not the same as prevID then mark this run completed
			if((ID != prevID || idx == Cells.Count() - 1) && prevID != -1)
			{
				runLengths.Add(currentRunLength);
				elementIDs.Add(ID);
				beginningIdxs.Add(idx);

				currentRunLength = 0;
				prevID = -1;
			}
			else
			{
				prevID = ID;
			}

			currentRunLength += 1;
			idx += 1;
		}

		// store the number of runs
		WorldFile.Store32((uint)runLengths.Count);

		// Loops over the compressed cells and stores their information
		idx = 0;
		foreach (int id in elementIDs)
		{
			
			// store the length of the run
			WorldFile.Store32((uint)runLengths[idx]);
			// store the index of the element
			WorldFile.Store16((ushort)id);

			// Store all cell-specific Fields
			foreach(int field in Cells[beginningIdxs[idx]].Fields)
			{
				WorldFile.Store8((byte)field);
			}


			idx += 1;
		}


	}

	/// <summary>
	/// File must be empty (the default .pwdr template).
	/// </summary>
	/// <param name="File"></param>
	private void StoreElementLookup()
	{
		// add the number of elements
		WorldFile.Store16((ushort)SandInfoCS.ElementResource.ElementOrder.Count);
		//add the element names
		foreach(string element in SandInfoCS.ElementResource.ElementOrder)
		{
			WorldFile.StorePascalString(element);
		}
	}

    
    private void StoreChunkFileOffsets()
    {
        
    }


    // stored data structs -------------------------

    private struct storedCellRun
	{
		public int RunLength;
		public short ElementIndex;
		public byte[] CellFields;

		public storedCellRun(int runLength, short elementIndex, byte[] cellFields)
		{
			RunLength = runLength;
			ElementIndex = elementIndex;

			CellFields = cellFields;
			
		}
	}

	private struct storedChunkInfo
	{

		public int X;
		public int Y;
		public uint CellRunNum;
		public List<storedCellRun> CellRuns;

		public storedChunkInfo(int XPosition, int YPosition, List<storedCellRun> cellRuns)
		{
			X = XPosition;
			Y = YPosition;

			CellRuns = cellRuns;

			CellRunNum = (uint)CellRuns.Count;
		}
	}

	private struct storedWorldInfo
	{
		
		public string Header;
		// number of elements in the stored lookup table.
		public short ElementNum;
		public List<string> ElementNames;
		public byte CellFieldNum;
		public int ChunkNum;
		public List<storedChunkInfo> Chunks;
		// the element lookup table that was stored in the file; needed incase the indexes of elements change and an old file is loaded
		public Dictionary<int, AllElements> IdxToElement;

		public storedWorldInfo(string header, List<string> elementNames, byte cellFieldNum, List<storedChunkInfo> chunks, Dictionary<int, AllElements> elementLookup)
		{
			Header = header;
			ElementNames = elementNames;
			CellFieldNum = cellFieldNum;
			Chunks = chunks;
			IdxToElement = elementLookup;

			// get amount of the variable things
			ElementNum = (short)ElementNames.Count;
			ChunkNum = Chunks.Count;
		}


		// Helpers ---------------------
		
		/// <summary>
		/// If the given <c>Position</c> does not exist in the stored chunks, creates a new chunk in the save data. If it does exist, replaces that chunk instance.
		/// </summary>
		/// <param name="Position"></param>
		/// <returns>Whether the operation was successful or not.</returns>
		public bool AddOrReplaceChunk(Vector2I Position, storedChunkInfo newChunkInfo)
		{

			// whether the chunk was found in the chunks list and was replaced.
			bool replace = false;

			// search for chunk
			int idx = 0;
			foreach(storedChunkInfo chunk in Chunks)
			{
				if (chunk.X == Position.X && chunk.Y == Position.Y)
				{
					Chunks[idx] = newChunkInfo;
					replace = true;
					break;
				}

				idx++;
			}

			if (replace == false)
			{
				Chunks.Add(newChunkInfo);
			}
			

			return true;
		}

	}


}