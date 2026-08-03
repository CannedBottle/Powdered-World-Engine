using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Dynamically creates labels during gameplay that show the position of each chunk at the location of said chunk. Mostly used for debug purposes.
/// </summary>
[GlobalClass]
public partial class ChunkPosVisualizer : Node2D
{

    [Export] PowderSimulation Simulation;

    [Export] float LabelScale = 1.0f;

    private Dictionary<Vector2I, Label> Labels = new Dictionary<Vector2I, Label>{};


    public override void _EnterTree()
    {
        base._EnterTree();

        if(Simulation == null)
        {
            return;
        }
        // assign chunk change signals
        Simulation.ChunkAdded += OnChunkAdded;
        Simulation.ChunkRemoved += OnChunkRemoved;


    }


    public override void _Ready()
    {
        base._Ready();

        
    }


    private void OnChunkAdded(Vector2I position)
    {
        Label newLabel = new Label();
        newLabel.Text = position.ToString();
        newLabel.GlobalPosition = (GetLabelOffset() + position * Simulation.IndividualChunkSize) * Simulation.PixelScale;
        newLabel.Scale = new Vector2(LabelScale, LabelScale);


        AddChild(newLabel);
        Labels.Add(position, newLabel);

    }


    private void OnChunkRemoved(Vector2I position)
    {
        Labels[position].QueueFree();
        Labels.Remove(position);
    }


    /// <summary></summary>
    /// <returns>the offset obtained from the simulation's position and chunksize, <b>NOT</b> accounting for pixel scale.</returns>
    private Vector2 GetLabelOffset()
    {
        return Simulation.GlobalPosition + new Vector2(Simulation.IndividualChunkSize / 2, Simulation.IndividualChunkSize / 2);
    }

}
