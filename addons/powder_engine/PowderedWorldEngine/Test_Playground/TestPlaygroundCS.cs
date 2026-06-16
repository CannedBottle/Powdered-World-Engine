using Godot;
using System;
using System.Collections.Generic;
using static Elements;

public partial class TestPlaygroundCS : Node2D
{
	// ----------------------------------------------------------------------------------------------------------------------------------
	// This script is completely for handling the placing of elements in the playground, it is not needed whatsoever for the simulation;
	// you just have to handle the placement yourself since most likely it will not be done with the mouse + keys.
	// ----------------------------------------------------------------------------------------------------------------------------------

	private PowderSimulation Sim;
	private Label Fps;
	private Label UTime;
	private Label DTime;
	private CheckButton DebugSwitch;


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
		Fps = GetNode<Label>("ui/fps");
		UTime = GetNode<Label>("ui/update time");
		DTime = GetNode<Label>("ui/draw time");
		DebugSwitch = GetNode<CheckButton>("ui/debugswitch");

		DisplayServer.WindowSetSize(DisplayServer.ScreenGetSize());

		DebugSwitch.ButtonPressed = Sim.DebugMode;

		DebugSwitch.Pressed += OnDebugSwitched;
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
			Sim.PlaceGroupElements(brushSize, MousePos / Sim.PixelScale, ElementKeys[KeysPressed[0]], ElementKeys[KeysPressed[0]] == AllElements.AIR);
		}

		Fps.Text = "fps: " + Engine.GetFramesPerSecond().ToString();
		UTime.Text = "u: " + Sim.UpdateTime.ToString();
		DTime.Text = "d: " + Sim.DrawTime.ToString();

		if (Input.IsActionPressed("Place"))
		{
			Sim.PlaceGroupElements(brushSize, MousePos / Sim.PixelScale, AllElements.SAND, false);
		}

		if (Input.IsActionPressed("Exit"))
		{
			GetTree().Quit();
		}

		Sim.UpdateSimulation();

	}

	private void OnDebugSwitched()
	{
		Sim.DebugMode = DebugSwitch.ButtonPressed;
		Sim.QueueRedraw();
	}

}
