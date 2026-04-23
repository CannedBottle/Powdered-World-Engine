extends Node2D

@onready var sim: PowderSimulation = $"powdered world"
@onready var fps: Label = $ui/fps
@onready var u_time: Label = $"ui/update time"
@onready var d_time: Label = $"ui/draw time"


func _ready() -> void:
	DisplayServer.window_set_size(DisplayServer.screen_get_size())

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(_delta: float) -> void:
	var mouse_pos = Vector2i(get_global_mouse_position())
	
	fps.text = str(Engine.get_frames_per_second())
	u_time.text = "u: " + str(sim.update_time)
	d_time.text = "d: " + str(sim.draw_time)
	
	if Input.is_action_pressed("Place"):
		@warning_ignore("integer_division")
		sim.place_group_elements(1, mouse_pos / sim.pixel_scale, SandInfo.Elements.SAND, false)
	if Input.is_action_pressed("Exit"):
		get_tree().quit()
	
	
	sim.update_simulation()
