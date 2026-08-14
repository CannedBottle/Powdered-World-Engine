#if TOOLS
using Godot;


[Tool]
public partial class PowderEnginePlugin : EditorPlugin
{

    private EditorDock _elementDock;


    public override void _EnablePlugin()
    {
        base._EnablePlugin();
        
    }

    public override void _DisablePlugin()
    {
        base._DisablePlugin();
        RemoveAutoloadSingleton("SandInfoCS");
    }

    // ---------- Editor Dock ------------- //

    public override void _EnterTree()
    {
        base._EnterTree();
        AddAutoloadSingleton("SandInfoCS", "res://addons/powder_engine/PowderedWorldEngine/Scripts/CSharp/SandInfoCS.cs");
        SandInfoCS.PluginVersion = GetPluginVersion();


        var _dock_scene = GD.Load<PackedScene>("res://addons/powder_engine/PowderedWorldEngine/Element Creation/ElementDockScene.tscn").Instantiate<Control>();

        _elementDock = new EditorDock();
        _elementDock.AddChild(_dock_scene);

        _elementDock.Title = "Elements";

        _elementDock.DefaultSlot = EditorDock.DockSlot.RightUr;

        _elementDock.AvailableLayouts = EditorDock.DockLayout.Floating | EditorDock.DockLayout.Vertical;

        AddDock(_elementDock);


    }


    public override void _ExitTree()
    {
        base._ExitTree();

        RemoveDock(_elementDock);

        _elementDock.QueueFree();
    }



}
#endif
