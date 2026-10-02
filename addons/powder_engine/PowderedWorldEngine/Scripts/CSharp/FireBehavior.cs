using Godot;
using static SandInfo;
using static Elements;
using Godot.Collections;

[Tool]
[GlobalClass]
public partial class FireBehavior : ElementBehavior
{
    
    public override Dictionary<string, byte> _DefaultFields()
    {
        return new Dictionary<string, byte>
        {
            {"R#MovesBeforeExpire", 25},
            {"R#random", 1},
			{"R#direction", 0},
			{"bumps", 0},
        };
    }
    


    public override void _OnCellUpdate(SandInfo.Cell cell, PowderSimulation sim)
    {
        
        if(cell.GetField("R#MovesBeforeExpire") <= 0)
        {
            cell.SetElement(AllElements.AIR);   
        }
        else
        {
            cell.SetField("R#MovesBeforeExpire", (byte)(cell.GetField("R#MovesBeforeExpire") - 1));
            
            RunGas(cell, sim);
        }

    }


}
