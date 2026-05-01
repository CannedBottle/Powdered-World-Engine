extends Node2D

@onready var sim: PowderSimulationCs = $PowderSimulationCs
@onready var fps: Label = $ui/fps
@onready var u_time: Label = $"ui/update time"
@onready var d_time: Label = $"ui/draw time"

@export var brush_size: int = 2

var ElementKeys: Dictionary[Key, SandInfoCS.Elements] = {
	KEY_S: SandInfoCS.Elements.SAND,
	KEY_A: SandInfoCS.Elements.AIR,
	KEY_W: SandInfoCS.Elements.WATER,
	KEY_Q: SandInfoCS.Elements.WALL,
}

var keys_pressed: Array = []

func _ready() -> void:
	DisplayServer.window_set_size(DisplayServer.screen_get_size())


func _input(event: InputEvent) -> void:
	if event is InputEventKey:
		
		if event.is_pressed():
			if ElementKeys.has(event.keycode) and not keys_pressed.has(event.keycode):
				keys_pressed.append(event.keycode)
		else:
			keys_pressed.erase(event.keycode)

func _process(_delta: float) -> void:
	var mouse_pos = Vector2i(get_global_mouse_position())
	
	#spawn elements using keys
	if keys_pressed.size() > 0:
		@warning_ignore("integer_division")
		sim.PlaceGroupElements(brush_size - 1, mouse_pos / sim.PixelScale, ElementKeys[keys_pressed[0]], ElementKeys[keys_pressed[0]] == SandInfoCS.Elements.AIR)
	
	fps.text = str(Engine.get_frames_per_second())
	u_time.text = "u: " + str(sim.UpdateTime)
	d_time.text = "d: " + str(sim.DrawTime)
	
	if Input.is_action_pressed("Place"):
		@warning_ignore("integer_division")
		sim.PlaceGroupElements(brush_size - 1, mouse_pos / sim.PixelScale, SandInfoCS.Elements.SAND, false)
	
	if Input.is_action_pressed("Exit"):
		get_tree().quit()
	
	
	sim.UpdateSimulation()
