#if TOOLS
using Godot;
using System;

[Tool]
public partial class PowderEnginePlugin : EditorPlugin
{
	public override void _EnterTree()
	{
		// Initialization of the plugin goes here.
	}

	public override void _ExitTree()
	{
		// Clean-up of the plugin goes here.
	}


    public override void _EnablePlugin()
    {
        base._EnablePlugin();
    }

    public override void _DisablePlugin()
    {
        base._DisablePlugin();
    }




}
#endif
