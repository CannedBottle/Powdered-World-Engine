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

	private Sprite2D Player;

	private static readonly float PLAYER_SPEED = 750.0f;

	private bool SimPaused = false;

	[Export] public int brushSize = 2;


	public Dictionary<Key, AllElements> ElementKeys = new Dictionary<Key, AllElements>
	{
		{Key.S, AllElements.SAND},
		{Key.A, AllElements.AIR},
		{Key.W, AllElements.WATER},
		{Key.Q, AllElements.WALL},
		{Key.D, AllElements.STONE},
		{Key.E, AllElements.ACID},
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

		Player = GetNode<Sprite2D>("player");

		

		DisplayServer.WindowSetSize(DisplayServer.ScreenGetSize());

		DebugSwitch.ButtonPressed = Sim.DebugMode;

		DebugSwitch.Pressed += OnDebugSwitched;
		PauseButton.Toggled += Pause;
		NextFrameButton.Pressed += ProgressFrame;
		AddChunk.Pressed += OnChunkAddPressed;
		RemoveChunk.Pressed += OnChunkRemovePressed;
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

		if (Input.IsActionPressed("Exit"))
		{
			GetTree().Quit();
		}

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


	private void OnDebugSwitched()
	{
		Sim.DebugMode = DebugSwitch.ButtonPressed;
		Sim.QueueRedraw();
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
