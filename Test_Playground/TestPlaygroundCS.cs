using Godot;
using System;
using System.Collections.Generic;
using static Elements;

public partial class TestPlaygroundCS : Node2D
{
	// ----------------------------------------------------------------------------------------------------------------------------------
	// This script is completely for handling the placing of elements in the playground, it is not needed whatsoever for the plugin itself;
	// you just have to handle the placement yourself since most likely it will not be done with the mouse + keys.
	// ----------------------------------------------------------------------------------------------------------------------------------

	private PowderSimulation Sim;
	private Label Fps;
	private Label UTime;
	private Label DTime;
	private Label MTime;
	private CheckButton DebugSwitch;
	private Button PauseButton;
	private Button NextFrameButton;
	private SpinBox Xedit;
	private SpinBox Yedit;
	private Button AddChunk;
	private Button RemoveChunk;
	private Button SaveButton;
	private Button LoadButton;
	private Button FlushButton;

	private CollisionComponent collisionComponent;
	private Button collisionInitButton;

	private ChunkPosVisualizer PosVisualizer;

	private Sprite2D Player;

	private static readonly float PLAYER_SPEED = 750.0f;

	private bool SimPaused = false;

	[Export] public int brushSize = 2;

	[Export] public bool GenerateNoiseWall = false;


	private WorldStreamer WorldSave;
		

	public Dictionary<Key, AllElements> ElementKeys = new Dictionary<Key, AllElements>
	{
		{Key.S, AllElements.SAND},
		{Key.A, AllElements.AIR},
		{Key.W, AllElements.WATER},
		{Key.Q, AllElements.WALL},
		{Key.D, AllElements.STONE},
		{Key.E, AllElements.ACID},
		{Key.C, AllElements.SMOKE},
		{Key.F, AllElements.FIRE},
	};

	private List<Key> KeysPressed = new List<Key>();
	
	public override void _Ready()
	{
		Sim = GetNode<PowderSimulation>("PowderSimulation");
		Fps = GetNode<Label>("ui/VBoxContainer/fps");
		UTime = GetNode<Label>("ui/VBoxContainer/update time");
		DTime = GetNode<Label>("ui/VBoxContainer/draw time");
		MTime = GetNode<Label>("ui/VBoxContainer/misc time");
		DebugSwitch = GetNode<CheckButton>("ui/VBoxContainer/debugswitch");
		PauseButton = GetNode<Button>("ui/VBoxContainer/pause");
		NextFrameButton = GetNode<Button>("ui/VBoxContainer/forward");
		Xedit = GetNode<SpinBox>("ui/VBoxContainer/CoordEdit/Xedit");
		Yedit = GetNode<SpinBox>("ui/VBoxContainer/CoordEdit/Yedit");
		AddChunk = GetNode<Button>("ui/VBoxContainer/ChunkEdit/AddChunk");
		RemoveChunk = GetNode<Button>("ui/VBoxContainer/ChunkEdit/RemoveChunk");
		SaveButton = GetNode<Button>("ui/VBoxContainer/SaveWorld");
		LoadButton = GetNode<Button>("ui/VBoxContainer/LoadWorld");
		FlushButton = GetNode<Button>("ui/VBoxContainer/Flush");
		PosVisualizer = GetNode<ChunkPosVisualizer>("ChunkPosVisualizer");

		collisionComponent = GetNode<CollisionComponent>("CollisionComponent");
		collisionInitButton = GetNode<Button>("ui/VBoxContainer/collisions");


		Player = GetNode<Sprite2D>("player");

		
		WorldSave = WorldStreamer.Open("user://pwengine/worlds/WorldTest.pwdr", Sim);
		if(WorldSave == null)
		{
			WorldSave = WorldStreamer.Create("user://pwengine/worlds/", "WorldTest", Sim);
		}
		
		Sim.SetWorldStreamer(WorldSave);

		DisplayServer.WindowSetSize(DisplayServer.ScreenGetSize());

		DebugSwitch.ButtonPressed = Sim.DebugMode;


		if (GenerateNoiseWall)
		{
			GenerateWallWorld();
		}


		DebugSwitch.Pressed += OnDebugSwitched;
		PauseButton.Toggled += Pause;
		NextFrameButton.Pressed += ProgressFrame;
		AddChunk.Pressed += OnChunkAddPressed;
		RemoveChunk.Pressed += OnChunkRemovePressed;
		SaveButton.Pressed += SaveWorld;
		LoadButton.Pressed += LoadWorld;
		FlushButton.Pressed += FlushFile;

		collisionInitButton.Pressed += collisionComponent.InitSegmentsFromWorld;
	}


    public override void _Input(InputEvent @event)
    {
        base._Input(@event);

		if(@event is InputEventKey inputKey)
		{

			if (inputKey.IsPressed())
			{
				if (ElementKeys.TryGetValue(inputKey.Keycode, out AllElements element) && !KeysPressed.Contains(inputKey.Keycode))
				{
					KeysPressed.Add(inputKey.Keycode);
				}
			}
			else
			{
				KeysPressed.Remove(inputKey.Keycode);
			}


		}
		
    }

	
	public override void _Process(double delta)
	{
		Vector2I MousePos = (Vector2I)GetGlobalMousePosition();

		// spawn elements using keys
		if(KeysPressed.Count > 0)
		{
			Sim.PlaceGroupElements(brushSize, Sim.WorldToLocal(MousePos), ElementKeys[KeysPressed[0]], ElementKeys[KeysPressed[0]] == AllElements.AIR);
		}

		Fps.Text = "fps: " + Engine.GetFramesPerSecond().ToString();
		UTime.Text = "u: " + Sim.UpdateTime.ToString();
		DTime.Text = "d: " + Sim.DrawTime.ToString();
		MTime.Text = "misc: " + Sim.MiscTime.ToString();

		if(!SimPaused)
		{
			Sim.UpdateSimulation();
		}


	}

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

		// handle player movement
		Vector2 velo = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		Player.Position += new Vector2(velo.X * PLAYER_SPEED * (float)delta,
		velo.Y * PLAYER_SPEED * (float)delta);
    }


	private void GenerateWallWorld()
	{
		FastNoiseLite WorldNoise = new FastNoiseLite();
		WorldNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
		WorldNoise.Seed = (int)GD.Randi();
		WorldNoise.Frequency = 0.02f;

		foreach(SandInfo.Chunk Chunk in Sim.Chunks.Values)
		{
			foreach(SandInfo.Cell Cell in Chunk.Cells)
			{
				if(WorldNoise.GetNoise2D(Cell.Position.X, Cell.Position.Y) > -0.5)
				{
					switch (WorldNoise.GetNoise2D(Cell.Position.X, Cell.Position.Y))
					{
						
						case > 0.6f:
							Sim.SetCell(Cell, AllElements.WATER);
							break;
						case > 0.3f:
							Sim.SetCell(Cell, AllElements.SAND);
							break;
						case > -0.2f:
							Sim.SetCell(Cell, AllElements.WALL);
							break;
						default:
							Sim.SetCell(Cell, AllElements.AIR);
							break;

					}
					
				}
			}
		}

	}

	private void OnDebugSwitched()
	{
		Sim.DebugMode = DebugSwitch.ButtonPressed;
		Sim.QueueRedraw();

		PosVisualizer.Visible = !PosVisualizer.Visible;
	}

	private void Pause(bool on)
	{
		SimPaused = on;
	}

	private void ProgressFrame()
	{
		if (SimPaused)
		{
			Sim.UpdateSimulation(false);
		}
	}

	private void SaveWorld()
	{
		WorldSave.SaveWorld(true);
	}

	private void LoadWorld()
	{
		WorldSave.LoadWorld();
	}

	private void FlushFile()
	{
		WorldSave.FlushDeadChunks();
	}

	// Chunk Addition + Removal UI ------------------------------


	private void OnChunkAddPressed()
	{
		Sim.AddChunk(GetTypedCoords());
	}


	private void OnChunkRemovePressed()
	{
		Sim.RemoveChunk(GetTypedCoords());
	}


	private Vector2I GetTypedCoords()
	{
		return new Vector2I((int)Xedit.Value, (int)Yedit.Value);
	}


}
