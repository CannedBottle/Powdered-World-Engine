using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static Elements;



/// <summary>
/// A class that handles the file I/O of saving + loading worlds (files with the <c>.pwdr</c> extension). Also contains helper functions for manipulating the connected <c>PowderSimulation</c> by loading chunks from the file.
/// </summary>
public partial class WorldStreamer : RefCounted
{
    
    // STATICS ----------------------------
	
    /// <summary>
    /// Creates a new WorldStreamer object and assigns it the specified file. <c>WorldPath</c> must point to an existing file with the <c>.pwdr</c> extension.
    /// </summary>
    /// <returns>a new <c>WorldStreamer</c> object if the file is usable; otherwise returns <c>null</c>.</returns>
    public static WorldStreamer Open(string WorldPath, PowderSimulation Simulation)
    {
		
        if (!IsFileUsable(WorldPath))
		{
			return null;
		}

		WorldStreamer newStreamer = new WorldStreamer(WorldPath, Simulation);

		if (newStreamer.HasFileOffsetDict())
		{
			newStreamer.SetOffsetDict();
		}

        return newStreamer;
    }


    /// <summary>
    /// Creates a new WorldStreamer object and a <c>.pwdr</c>file with the given <c>FileName</c> assigned to it. <c>WorldDirPath</c> must point to a directory in which the file can be created. Usually starts with <c>user://</c>.
	/// <b>FileName MUST be different than an already existing file. Otherwise it opens the existing file.</b>
    /// </summary>
    /// <param name="WorldPath">The path to the folder for this file to be saved in. Usually starts with <c>user://</c>.</param>
    /// <param name="FileName">The name for the new file to be called. The extension is automatically attached, so no need to add <c>.pwdr</c> to the end of this parameter.</param>
    /// <returns>Returns a new <c>WorldStreamer</c> object if the filename and directory path are usable; otherwise returns <c>null</c>.</returns>
    public static WorldStreamer Create(string WorldDirPath, string FileName, PowderSimulation Simulation)
    {
        return new WorldStreamer(SaveEmpty(FileName, WorldDirPath), Simulation);
    }


    /// <summary>
	/// Creates an empty save file. <c>Path</c> must point to a directory in which the new file can be added to. <c>Path</c> also must end with <c>/</c>. <b>Do not</b> include the file extension in <c>Name</c>.
	/// </summary>
	/// <param name="Name">The name to be given to the newly created file. The file extension is automatically attached, so no need to add <c>.pwdr</c> to the end of this parameter.</param>
	/// <param name="Path">The path to the folder for this file to be saved in. Usually starts with <c>user://</c>. <b>Must</b> end with <c>/</c>.</param>
    /// <returns>the path to the newly created file.</returns>
	private static string SaveEmpty(string Name, string Path)
	{

        string fullPath = Path + Name + "." + PowderSimulation.DefaultFileExtension;

		DirAccess.MakeDirRecursiveAbsolute(Path);

		using var worldFile = FileAccess.Open(fullPath, FileAccess.ModeFlags.Write);

		worldFile.StorePascalString("PWEngine " + SandInfo.PluginVersion);

		// store file position placeholder for offset dict
		worldFile.Store32(0);

		// store element num placeholder (0)
		worldFile.Store16(0);

		// store cell field num placeholder (0)
		worldFile.Store8(0);

		// store chunk num placeholder (0)
		worldFile.Store32(0);

		worldFile.Close();
		worldFile.Dispose();

        return fullPath;
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


    // --------------------------

    private WorldStreamer(string Path, PowderSimulation Simulation)
    {
        Sim = Simulation;

		Sim.ChunkChanged += FilterChangedChunk;

        WorldFile = FileAccess.Open(Path, FileAccess.ModeFlags.ReadWrite);

		ElementLookup = GetElementLookup();

		// save world if the file was just created
		if (!HasFileOffsetDict())
		{
			SaveWorld(true);
		}
		
    }

	public Dictionary<int, AllElements> ElementLookup;

	private List<Vector2I> ChunksChangedSinceSave = new List<Vector2I>{};

    private PowderSimulation Sim;

    private FileAccess WorldFile;


    private Dictionary<Vector2I, ulong> ChunkFileOffsets = new Dictionary<Vector2I, ulong>{};



	// PUBLIC ------------------------------------------------------


	/// <summary>
	/// Flushes dead chunks, closes the file, and disposes of this object.
	/// <br/> <b>Note:</b> WorldStreamer will automatically close when it's freed, which happens when it goes out of scope or when it gets assigned with null. 
	/// In C# the reference must be disposed after we are done using it, this can be done with the <c>using</c> statement or calling the <c>Dispose</c> method directly.
	/// </summary>
	public void Close()
	{
		FlushDeadChunks();

		WorldFile.Close();

		Dispose();		
	}


	/// <summary>
	/// Reads through the file and removes saved chunks that are duplicates. Uses <c>ChunkFileOffsets</c> to determine duplicates. 
	/// <br/>
	/// <b>Will most likely fix a corrupted file.</b> 
	/// <br/>
	/// Automatically called when <c>Close</c> is called. Should not be called often, as it rewrites the whole file.
	/// </summary>
	/// <returns>Whether or not there were any dead chunks to remove.</returns>
	public bool FlushDeadChunks()
	{
		// use GetFileData and check against the filePosition var i added to the chunk struct

		storedWorldInfo worldData = GetFileData().Value;

		// whether there were any dead chunks to remove. used for return val.
		bool RemovedChunks = false;

		Stack<int> idxToRemove = new Stack<int>{};

		int idx = 0;
		foreach (storedChunkInfo chunkInfo in worldData.Chunks)
		{
			Vector2I pos = new Vector2I(chunkInfo.X, chunkInfo.Y);

			// check if value exists; otherwise continue
			if(!ChunkFileOffsets.TryGetValue(pos, out ulong testFilePos))
			{
				idx++;
				continue;
			}

			if (chunkInfo.filePosition != testFilePos)
			{
				idxToRemove.Push(idx);
			}

			idx++;
		}

		// remove the duplicate chunks
		foreach (int index in idxToRemove)
		{
			worldData.Chunks.RemoveAt(index);
		}

		// save the file
		SaveFromData(worldData);

		WorldFile.Flush();

		return RemovedChunks;
	}


	/// <summary>
	/// Saves the world to disk, only operating on the chunks currently active in the simulation.
	/// </summary>
	/// <param name="Override">Whether or not to override the contents of the file, essentially replacing the contents with the current ones.</param>
	/// <returns>Whether the operation was successful or not.</returns>
	public bool SaveWorld(bool Override)
	{

		if(Override)
		{
			WorldFile.Seek(0);

			WorldFile.GetPascalString();
			WorldFile.Get32();
			WorldFile.Resize((long)WorldFile.GetPosition());

			// element lookup table --------------
			StoreElementLookup();

			// store the number of cell-specific fields every cell has
			WorldFile.Store8((byte)SandInfo.Cell.FieldCount);

			// store number of chunks
			WorldFile.Store32((uint)Sim.Chunks.Count);
			
			// store chunks
			foreach(SandInfo.Chunk chunk in Sim.Chunks.Values)
			{
				StoreChunk(chunk);
			}

			StoreFileOffsetDict();
		}
		else // --------------------------------------------------------------
		{
			WorldFile.Seek(0);
			// advance to chunk num
			WorldFile.GetPascalString();
			// get file offset dict position
			ulong dictOffset = WorldFile.Get32();
			ushort elementNum = WorldFile.Get16();
			for(int i = 0; i < elementNum; i++)
			{
				WorldFile.GetPascalString();
			}
			WorldFile.Get8();

			// update chunk num
			ulong chunkNumFilePos = WorldFile.GetPosition();
			uint oldChunkNum = WorldFile.Get32();
			WorldFile.Seek(chunkNumFilePos);
			// add newly changed chunks to the file chunk count
			WorldFile.Store32(oldChunkNum + (uint)ChunksChangedSinceSave.Count);

			// store changed chunks as new ones and reassign them in ChunkFileOffsets
			WorldFile.Seek(dictOffset);
			// remove chunk offset dict
			WorldFile.Resize((long)dictOffset);
			// store each new chunk, updating
			foreach (Vector2I changedPos in ChunksChangedSinceSave)
			{
				StoreChunk(Sim.GetChunk(changedPos));
			}

			// add updated dict back
			StoreFileOffsetDict();

		}

		return true;
	}

	/// <summary>
	/// Saves the given chunk to the file. Creates a dead chunk in the file if there was an old version already saved. The dead chunk can be removed by calling <see cref="FlushDeadChunks"/>.
	/// </summary>
	/// <param name="chunk"></param>
	public void SaveChunk(SandInfo.Chunk chunk)
	{
		WorldFile.Seek(0);
		// advance to chunk num
		WorldFile.GetPascalString();
		// get file offset dict position
		ulong dictOffset = WorldFile.Get32();
		ushort elementNum = WorldFile.Get16();
		for (int i = 0; i < elementNum; i++)
		{
			WorldFile.GetPascalString();
		}
		WorldFile.Get8();

		// update chunk num
		ulong chunkNumFilePos = WorldFile.GetPosition();
		uint oldChunkNum = WorldFile.Get32();
		WorldFile.Seek(chunkNumFilePos);
		// add newly changed chunks to the file chunk count
		WorldFile.Store32(oldChunkNum + 1);

		// store changed chunks as new ones and reassign them in ChunkFileOffsets
		WorldFile.Seek(dictOffset);
		// remove chunk offset dict
		WorldFile.Resize((long)dictOffset);
		// store new chunk
		StoreChunk(chunk);

		// add updated dict back
		StoreFileOffsetDict();
	}

	// loading................

	/// <summary>
	/// Replaces the current active chunks in the simulation with the corresponding saved chunks in the file. <b>Does not create chunks.</b>
	/// </summary>
	/// <returns>Whether or not there were any currently loaded chunks saved in the file.</returns>
	public bool LoadWorld()
	{
		// make sure the lookup is up to date
		ElementLookup = GetElementLookup();

		int ChunksInFile = 0;

		foreach(Vector2I chunkPos in Sim.Chunks.Keys)
		{
			if (ChunkFileOffsets.ContainsKey(chunkPos))
			{
				ChunksInFile++;
			}

			LoadChunk(chunkPos);
		}

		return ChunksInFile > 0;
	}

	/// <summary>
	/// Saves the given Chunk to the file, and removes it from the simulation. Uses <see cref="SaveChunk"/> to save to file, so everything from that description applies here.
	/// </summary>
	/// <param name="Chunk"></param>
	public void UnloadChunk(SandInfo.Chunk Chunk)
	{
		SaveChunk(Chunk);

		Sim.RemoveChunk(Chunk.ChunkPosition);
	}

	/// <summary>
	/// Replaces or adds a chunk to the simulation with data from the file.
	/// </summary>
	/// <param name="ChunkPosition"></param>
	/// <returns>Whether the specified chunk position exists in the file.</returns>
	public bool LoadChunk(Vector2I ChunkPosition)
	{
		// check if the position exists in the file
		if(!ChunkFileOffsets.TryGetValue(ChunkPosition, out ulong filePos))
		{
			return false;
		}
		
		SandInfo.Chunk editChunk;

		// set editChunk to an existing chunk, otherwise create one
		if (Sim.Chunks.ContainsKey(ChunkPosition))
		{
			editChunk = Sim.GetChunk(ChunkPosition);
		}
		else
		{
			Sim.AddChunk(ChunkPosition);
			editChunk = Sim.GetChunk(ChunkPosition);
		}

		storedChunkInfo chunkData = GetChunkData(ChunkPosition).Value;

		// edit cell data
		int cellIndex = 0;
		foreach (storedCellRun run in chunkData.CellRuns)
		{

			for(int i = 0; i < run.RunLength; i++)
			{

				SandInfo.Cell cell = editChunk.Cells[cellIndex];

				cell.Element = ElementLookup[run.ElementIndex];
				cell.Fields = run.CellFields;

				editChunk.MarkCellUpdated(cell);

				cellIndex++;
			}
		}

		return true;

	}

	/// <summary></summary>
	/// <returns>A Godot Array containing the positions of all chunks saved in the file.</returns>
	public Godot.Collections.Array<Vector2I> GetAvailableChunks()
	{
		storedWorldInfo worldData = GetFileData().Value;

		Godot.Collections.Array<Vector2I> returnArr = new Godot.Collections.Array<Vector2I>{};

		foreach(storedChunkInfo chunkData in worldData.Chunks)
		{
			returnArr.Add(new Vector2I(chunkData.X, chunkData.Y));
		}

		return returnArr;
	}

    // OVERRIDE ----------------------------------------------------------


    public override void _Notification(int what)
    {

		// close before object is freed
		if(what == NotificationPredelete)
		{
			Close();
		}

        base._Notification(what);
    }


	// PRIVATE -----------------------------------------------------------

	/// <summary>
	/// connected to PowderSim Action OnChunkChanged
	/// </summary>
	/// <param name="chunkPos"></param>
	private void FilterChangedChunk(Vector2I chunkPos)
	{
		if (!ChunksChangedSinceSave.Contains(chunkPos))
		{
			ChunksChangedSinceSave.Add(chunkPos);
		}
	}

	private bool HasFileOffsetDict()
	{
		WorldFile.Seek(0);

		WorldFile.GetPascalString();

		return WorldFile.Get32() != 0;
	}


	private void RemoveFileOffsetDict()
	{

		if (!HasFileOffsetDict())
		{
			return;
		}

		WorldFile.Seek(0);

		WorldFile.GetPascalString();

		long offset = WorldFile.Get32();

		WorldFile.Resize(offset);

	}

	/// <summary>
	/// Extracts the information from a <c>.pwdr</c> file into a <c>storedWorldInfo</c> struct so the information can be used.
	/// </summary>
	/// <param name="Path"></param>
	/// <returns>A <c>storedWorldInfo</c> struct that represents the entire contents of the file. Returns <c>null</c> if the file is unusable.</returns>
	private storedWorldInfo? GetFileData()
	{
		WorldFile.Seek(0);

		// get header
		string header = WorldFile.GetPascalString();

		// advance past the offest dict location integer
		WorldFile.Get32();

		// generate element lookup
		Dictionary<int, AllElements> IdxToElement = new Dictionary<int, AllElements>{};

		List<string> elementNames = new List<string>();

		ushort elementNum = WorldFile.Get16();
		for (int i = 0; i < elementNum; i++)
		{
			string element = WorldFile.GetPascalString();
			IdxToElement.Add(i, Elements.GetFromName(element));

			elementNames.Add(element);
		}

		// collect information required to create the storedWorldInfo instance
		byte cellFields = WorldFile.Get8();

		List<storedChunkInfo> chunkData = new List<storedChunkInfo>();
		uint chunkNum = WorldFile.Get32();
		// loop over chunks
		for (int i = 0; i < chunkNum; i++)
		{
			ulong filePos = WorldFile.GetPosition();

			uint xPos = WorldFile.Get32();
			uint yPos = WorldFile.Get32();

			List<storedCellRun> cellRuns = new List<storedCellRun>();

			uint runNum = WorldFile.Get32();

			// get cell runs
			for (int run = 0; run < runNum; run++)
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
			chunkData.Add(new storedChunkInfo((int)xPos, (int)yPos, cellRuns, filePos));

		}

		// does not get the chunk offset dict, its unnecessary


		// finally create and return the world info instance

		return new storedWorldInfo(header, elementNames, cellFields, chunkData, IdxToElement);

	}


	private storedChunkInfo? GetChunkData(Vector2I ChunkPosition)
	{
		if (!ChunkFileOffsets.TryGetValue(ChunkPosition, out ulong filePos))
		{
			return null;
		}

		byte cellFields = GetCellFields();

		WorldFile.Seek(filePos);

		uint xPos = WorldFile.Get32();
		uint yPos = WorldFile.Get32();

		List<storedCellRun> cellRuns = new List<storedCellRun>();

		uint runNum = WorldFile.Get32();

		// get cell runs
		for (int run = 0; run < runNum; run++)
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
			return new storedChunkInfo((int)xPos, (int)yPos, cellRuns, filePos);

	}

	/// <summary></summary>
	/// <returns>The number of cell-specific fields each cell has in the file.</returns>
	private byte GetCellFields()
	{
		// advance to it
		WorldFile.Seek(0);
		WorldFile.GetPascalString();
		WorldFile.Get32();
		ushort elementNum = WorldFile.Get16();
		for(int i = 0; i < elementNum; i++)
		{
			WorldFile.GetPascalString();
		}

		return WorldFile.Get8();

	}

	private Dictionary<int, AllElements> GetElementLookup()
	{
		WorldFile.Seek(0);
		WorldFile.GetPascalString();
		WorldFile.Get32();

		Dictionary<int, AllElements> returnDict = new Dictionary<int, AllElements>{};
		
		ushort elementNum = WorldFile.Get16();

		for(int i = 0; i < elementNum; i++)
		{
			returnDict.Add(i, Elements.GetFromName(WorldFile.GetPascalString()));
		}


		return returnDict;
	}

	/// <summary>
	/// Saves the data within <c>Data</c> to a <c>.pwdr</c> file while overriding the previous contents.
	/// </summary>
	/// <param name="Data"></param>
	/// <returns>Whether the operation was successful or not.</returns>
	private bool SaveFromData(storedWorldInfo Data)
	{
		// refresh element lookup
		ElementLookup = GetElementLookup();

		WorldFile.Seek(0);

		// keep header + file offset pointer
		WorldFile.GetPascalString();
		WorldFile.Get32();
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
			// update this chunk's file offset
			ChunkFileOffsets[new Vector2I(chunk.X, chunk.Y)] = WorldFile.GetPosition();

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

		WorldFile.SeekEnd();
		StoreFileOffsetDict();

		return true;
	}

	/// <summary>
	/// stores a chunk's worth of data at the pointer of <c>File</c>.
	/// </summary>
	/// <param name="Chunk"></param>
	private void StoreChunk(SandInfo.Chunk Chunk)
	{
		ChunkFileOffsets[Chunk.ChunkPosition] = WorldFile.GetPosition();

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
	private void CompressAndStoreCells(SandInfo.Cell[] Cells)
	{
		List<StringName> elementOrder = SandInfo.ElementResource.ElementOrder.ToList();

		List<int> runLengths = new List<int>();
		List<int> elementIDs = new List<int>();
		// stores the index of the first element in each run to use for collecting cell-specific data
		List<int> beginningIdxs = new List<int>();

		int currentRunLength = 0;
		int prevID = -1;
		int idx = 0;
		foreach(SandInfo.Cell cell in Cells)
		{
			// get the unique ID of the element name
			//int ID = elementOrder.IndexOf(Enum.GetName(cell.Element));
			int ID = ElementLookup.FirstOrDefault(x => x.Value == cell.Element).Key;

			// if ID is not the same as prevID then mark this run completed
			if((ID != prevID || idx == Cells.Count() - 1) && prevID != -1)
			{
				runLengths.Add(currentRunLength);
				elementIDs.Add(prevID);
				beginningIdxs.Add(idx - 1);

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
	private void StoreElementLookup()
	{
		// add the number of elements
		WorldFile.Store16((ushort)SandInfo.ElementResource.ElementOrder.Count);
		
		//add the element names
		foreach(string element in SandInfo.ElementResource.ElementOrder)
		{
			WorldFile.StorePascalString(element);
		}
	}

    
    private void StoreFileOffsetDict()
    {
		// does not store number of offsets since it uses the number of chunks saved before.


		ulong pointerpos = WorldFile.GetPosition();

		// store chunk num
		WorldFile.Store32((uint)ChunkFileOffsets.Count());

		// save each key/value pair
		foreach (Vector2I pos in ChunkFileOffsets.Keys)
		{
			//store x and y position separately
			WorldFile.Store32((uint)pos.X);
			WorldFile.Store32((uint)pos.Y);

			//store file offset
			WorldFile.Store32((uint)ChunkFileOffsets[pos]);

		}


		// change the 32-bit integer to point to this dict
		WorldFile.Seek(0);
		// advance past header
		WorldFile.GetPascalString();
		WorldFile.Store32((uint)pointerpos);

    }




	/// <summary>
	/// Reads the offset dict in this streamer's file, and uses that to make the offset dict in local memory.
	/// </summary>
	private void SetOffsetDict()
	{
		WorldFile.Seek(0);
		// advance past header
		WorldFile.GetPascalString();

		uint offsetDictPos = WorldFile.Get32();
		
		WorldFile.Seek(offsetDictPos);

		// get number of chunks
		uint chunkNum = WorldFile.Get32();

		// generates offset dict
		ChunkFileOffsets.Clear();
		for(int i = 0; i < chunkNum; i++)
		{
			uint X = WorldFile.Get32();
			uint Y = WorldFile.Get32();
			uint fileOffset = WorldFile.Get32();

			// insert stuff
			ChunkFileOffsets[new Vector2I((int)X, (int)Y)] = fileOffset;
		}

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

		public ulong filePosition;

		public storedChunkInfo(int XPosition, int YPosition, List<storedCellRun> cellRuns, ulong filePos)
		{
			X = XPosition;
			Y = YPosition;

			CellRuns = cellRuns;

			filePosition = filePos;

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

		/* UNUSED
		// Helpers ---------------------
		
		/// <summary>
		/// If the given <c>Position</c> does not exist in the stored chunks, creates a new chunk in the save data. If it does exist, replaces that chunk instance.
		/// </summary>
		/// <param name="Position"></param>
		/// <returns>Whether the operation was successful or not.</returns>
		private bool AddOrReplaceChunk(Vector2I Position, storedChunkInfo newChunkInfo)
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
		*/

	}


}