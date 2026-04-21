extends TextureRect
class_name chunk_renderer

@export var chunk_size: int = 30
@export var pixel_scale: int = 10
@export var show_debug_info: bool = false

const render_shader: Shader = preload("uid://dtywnulygwh48")
const debug_square_stylebox_ref: String = "uid://doj0g3bw0g0ui"

var cell_values: PackedVector4Array

var debug_info: Control 

signal cells_updated

func _ready() -> void:
	cells_updated.connect(_cells_updated)
	
	#Creates an appropriately-sized texture for the shader to work with. Gradient is the easiest. I'm lazy.
	var gradient: Gradient = Gradient.new()
	gradient.remove_point(0)
	var text: GradientTexture2D = GradientTexture2D.new()
	text.width = chunk_size
	text.height = chunk_size
	text.gradient = gradient
	texture = text
	
	#Creates a Panel with a Stylebox as a debug visual
	if show_debug_info:
		debug_info = Control.new()
		debug_info.name = "debug info"
		add_child(debug_info)
		
		var debug_square: Panel = Panel.new()
		debug_square.custom_minimum_size = Vector2(chunk_size, chunk_size)
		debug_square.add_theme_stylebox_override("panel", load(debug_square_stylebox_ref))
		debug_info.add_child(debug_square)
	
	
	#Creates a shadermaterial with grid displace shader
	set_material(ShaderMaterial.new())
	material.shader = render_shader
	
	scale = Vector2(pixel_scale, pixel_scale)
	custom_minimum_size = Vector2(chunk_size, chunk_size)
	cell_values.resize(int(pow(chunk_size, 2)))
	cells_updated.emit()


func _cells_updated():
	material.set_shader_parameter("cells", cell_values)
