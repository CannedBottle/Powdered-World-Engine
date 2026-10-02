using Godot;

[Tool]
[GlobalClass]
public abstract partial class ElementBehavior : Resource
{
    /// <summary>
    /// Override this function if your custom element behavior requires storing data across update cycles.<br/>
    /// <b>Note:</b> ONLY RETURN A DICTIONARY WITH A SIZE OF LESS THAN OR EQUAL TO 8; anything else will throw an error, as well as mess up saving and loading. <br/><br/>
    /// 
    /// If you want one of your fields to be randomized on a per-cell basis upon initialization:<br/>
    ///  - Start the name of the field with "R#" <br/>
    ///  - The minimum random value will be zero; the max value will be the value returned from this dictionary.
    /// </summary>
    /// <returns>A new Dictionary where the keys are the name used to access the field and the values are the default values of the fields.</returns>
    public virtual Godot.Collections.Dictionary<string, byte> _DefaultFields()
    {
        return new Godot.Collections.Dictionary<string, byte>{};
    }

    /// <summary>
    /// Run for each cell of this element every tick. 
    /// </summary>
    /// <param name="cell">The cell (or element/pixel) is the object that represents one pixel in the simulation. Contains methods like TryMove for manipulating the element.</param>
    /// <param name="sim">The reference to the PowderSimulation Node that owns the cell.</param>
    public virtual void _OnCellUpdate(SandInfo.Cell cell, PowderSimulation sim) { }

}
