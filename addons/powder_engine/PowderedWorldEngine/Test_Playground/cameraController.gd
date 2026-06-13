extends Camera2D


#// ------- CAMERA DRAG ----------- //
@export var use_camera_drag: bool = false
var drag_offset: Vector2
var camera_origin: Vector2
var dragging := false

func _ready() -> void:
	camera_origin = global_position

func _input(event):
	if use_camera_drag:
		if event is InputEventMouseButton:
			if event.button_index == MOUSE_BUTTON_MIDDLE:
				dragging = event.pressed

		if event is InputEventMouseMotion and dragging:
			global_position -= event.relative
