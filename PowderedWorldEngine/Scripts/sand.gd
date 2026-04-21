extends Node2D
class_name simulation

@onready var fps: Label = $ui/fps
@onready var u_time: Label = $"ui/update time"
@onready var d_time: Label = $"ui/draw time"

## Shows chunks and dirty rects.
@export var debug_mode: bool = false
##How big the pixel appear on the screen.
@export var pixel_scale: int = 10
@export var simulation_speed: float = 0.5
##How many pixels (on each side) each chunk contains.
##IF CHANGING HIGHER THAN VALUE IN CHUNKRENDER SHADER, CHANGE THE SIZE OF THE ARRAY IN THE CHUNKRENDER SHADER ASWELL
@export var individual_chunk_size: int = 64
##how many chunks to be used in the entire simulation.
@export var chunk_grid_size: Vector2i = Vector2i(1, 1)
##How many update loops with no change have to occur before sleeping the chunk.
@export var chunk_insomnia: int = 1


var simulation_size: Vector2i = Vector2i(128, 72)

#the amount of time it takes to update and draw, respectively. used for debug purposes.
var update_time: float = 0.0
var draw_time: float = 0.0


#update 60 times/sec
var sim_dt: float = 1.0 / 60.0
#time passed since startup
var accumulator: float = 0.0
#number of ticks passed since startup, mod by 2
var ticks_passed: int = 1

var active_chunk_renderers: Array[chunk_renderer]
var chunk_renderer_parent: Node2D

var chunks: Dictionary[Vector2i, SandInfo.Chunk]
var cells: Dictionary[Vector2i, SandInfo.Cell]
## for cells that need to be redrawn, after update
var cells_to_update: Dictionary[Vector2i, SandInfo.Cell]

func init_grid():
	chunks.clear()
	
	for y in chunk_grid_size.y:
		for x in chunk_grid_size.x:
			chunks.get_or_add(Vector2i(x, y), SandInfo.Chunk.new(individual_chunk_size, Vector2i(x, y), self, chunk_insomnia))
			
			var render = chunk_renderer.new()
			render.chunk_size = individual_chunk_size
			render.pixel_scale = pixel_scale
			render.position = Vector2i(x, y) * individual_chunk_size * pixel_scale
			if debug_mode:
				render.show_debug_info = true
			
			chunk_renderer_parent.add_child(render)
			active_chunk_renderers.append(render)
			
			chunks[Vector2i(x, y)].renderer = render
	

func update_chunks(invert_checkerboard: bool = false):
	update_time = Time.get_ticks_usec() / 1000.0
	#--------------------------------------------------------------
	for chunk in chunks.values():
		chunk.update_half_cells(invert_checkerboard)
	#--------------------------------------------------------------
	update_time = Time.get_ticks_usec() / 1000.0 - update_time

func render_chunk_updates():
	draw_time = Time.get_ticks_usec() / 1000.0
	#--------------------------------------------------------------
	for chunk in chunks.values():
		chunk.send_draw_info_to_renderer()
	#--------------------------------------------------------------
	draw_time = Time.get_ticks_usec() / 1000.0 - draw_time

func place_element(pos: Vector2i, element: SandInfo.Elements, override: bool = false) -> bool:
	if (pos.x < 0 or pos.y < 0 or pos.x > simulation_size.x - 1 or pos.y > simulation_size.y - 1):
		return false
	
	@warning_ignore("integer_division")
	#pos of chunk the cell being placed is in
	var chunk_position: Vector2i = pos / individual_chunk_size
	var chunk: SandInfo.Chunk = chunks[chunk_position]
	chunk.wake()
	
	if override == false and chunk.cells[pos].element != SandInfo.Elements.AIR:
		return false
	else:
		chunk.cells[pos].element = element
		chunk.updated_cells.get_or_add(pos, chunk.cells[pos])
		chunk.cells[pos].create_element_class()
		return true

func place_group_elements(brushsize: int, pos: Vector2i, element: SandInfo.Elements, override: bool = false):
	for y in brushsize * 2 + 1:
		for x in brushsize * 2 + 1:
			place_element(pos + Vector2i(x - brushsize, y - brushsize), element, override)


func _ready() -> void:
	DisplayServer.window_set_size(DisplayServer.screen_get_size())
	
	simulation_size = Vector2(chunk_grid_size.x * individual_chunk_size - 1, chunk_grid_size.y * individual_chunk_size - 1)
	
	chunk_renderer_parent = Node2D.new()
	self.add_child(chunk_renderer_parent)
	
	init_grid()

func _process(delta: float) -> void:
	var mouse_pos = Vector2i(get_global_mouse_position())
	
	fps.text = str(Engine.get_frames_per_second())
	u_time.text = "u: " + str(update_time)
	d_time.text = "d: " + str(draw_time)
	
	if Input.is_action_pressed("Place"):
		@warning_ignore("integer_division")
		place_group_elements(1, mouse_pos / pixel_scale, SandInfo.Elements.SAND, false)
	if Input.is_action_pressed("Exit"):
		get_tree().quit()
	
	accumulator += delta * simulation_speed
	
	while accumulator >= sim_dt:
		if ticks_passed == 0:
			update_chunks(true)
		else:
			update_chunks(false)
		
		render_chunk_updates()
		
		ticks_passed += 1
		ticks_passed %= 2
		accumulator -= sim_dt
		
	
