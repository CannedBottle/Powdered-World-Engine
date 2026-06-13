#if TOOLS
using Godot;
using System;

[Tool]
public partial class PowderEnginePlugin : EditorPlugin
{

    public override void _EnablePlugin()
    {
        base._EnablePlugin();
        AddAutoloadSingleton("SandInfoCS", "res://addons/powder_engine/PowderedWorldEngine/Scripts/CSharp/SandInfoCS.cs");
    }

    public override void _DisablePlugin()
    {
        base._DisablePlugin();
        RemoveAutoloadSingleton("SandInfoCS");
    }




}
#endif
