@icon("uid://b0ol6juljiyfp")
extends Node2D
##Used to create and handle falling sand simulations from Powdered World Engine.
##
##Refer to the official [url=https://placeholder.com]documentation[/url] for instructions on how to use.
##Do [b]not[/b] edit the script auto_attached to the node; extend it to add your own functionality.
class_name PowderSimulation

## Creates a panel for each chunk to show the chunks and modulates them depending on if they're asleep or not, and shows dirty rects.
@export var debug_mode: bool = false
## Creates a Panel with the dimensions of the simulation, as to show the border.
@export var show_sim_border: bool = false
##How big the pixels appear on the screen.
@export var pixel_scale: int = 10
##How fast the simulation runs, with 1.0 corresponding with 60 updates/second.
@export var simulation_speed: float = 1.0
##Checkerboard updates is a way of updating chunks in a checkerboard fashion, skipping every other cell. 
## Setting this to [code]false[/code] iterates without skipping.
@export var use_checkerboard_updates: bool = false
## Dirty rects are rectangles calculated at runtime that only contain the cells that were updated in the last frame, lowering the total number of cells needing updates.
## [br]
## [br]
## [b]Note:[/b] Recommended for chunk sizes 32 and above.
@export var use_dirty_rects: bool = true
##How many pixels (on each side) each chunk contains.
##[b]IF CHANGING HIGHER THAN VALUE IN CHUNKRENDER SHADER,[/b] change the size of the array in the chunkrender shader as well.
@export_range(1, 9223372036854775807) var individual_chunk_size: int = 64:
	set(value):
		if not is_inside_tree():
			individual_chunk_size = value
		else:
			push_warning("Attempted to change individual_chunk_size, when it should only be changed in the Inspector.")

##how many chunks to be used in the entire simulation.
@export var chunk_grid_size: Vector2i = Vector2i(1, 1):
	set(value):
		chunk_grid_size = value
		update_simulation_size()

##How many update loops with no change have to occur before sleeping the chunk.
@export var chunk_insomnia: int = 1


var simulation_size: Vector2i = Vector2(chunk_grid_size.x * individual_chunk_size - 1, chunk_grid_size.y * individual_chunk_size - 1)

#the amount of time it takes to update and draw, respectively. used for debug purposes.
var update_time: float = 0.0
var draw_time: float = 0.0
#will be null if show_sim_border is set to true
var border_ref: Panel
var border_style_uid: String = "uid://c1f2cxlbh1s74"

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
		chunk.update_half_cells(invert_checkerboard, use_checkerboard_updates)
		if use_dirty_rects:
			chunk.update_dirty_rect()
	#--------------------------------------------------------------
	update_time = Time.get_ticks_usec() / 1000.0 - update_time
	if use_dirty_rects and debug_mode:
		queue_redraw()

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


func _create_border(size: int):
	border_ref = Panel.new()
	var style: StyleBoxFlat = load(border_style_uid)
	style.border_width_left = size
	style.border_width_top = size
	style.border_width_right = size
	style.border_width_bottom = size
	
	border_ref.add_theme_stylebox_override("panel", style)
	
	border_ref.size = (simulation_size * pixel_scale) + Vector2i(size * 2, size * 2) + Vector2i(pixel_scale, pixel_scale)
	border_ref.position = -Vector2i(size, size)
	border_ref.name = "Border"
	add_child(border_ref)

##Updates the simulation_size variable if chunk grid size has been changed. This is automatically done, so no need to call this.
func update_simulation_size():
	simulation_size = Vector2(chunk_grid_size.x * individual_chunk_size - 1, chunk_grid_size.y * individual_chunk_size - 1)

func _ready() -> void:
	
	#make border if set to do so
	if show_sim_border:
		_create_border(10)
	
	chunk_renderer_parent = Node2D.new()
	self.add_child(chunk_renderer_parent)
	
	init_grid()

#used to draw dirty rects for debug purposes.
func _draw() -> void:
	var centering_val = Vector2i(1, 1) * (pixel_scale)
	for chunk in chunks.values():
		if chunk.has_valid_dirty_rect:
			var size: Vector2i = chunk.dirty_rect_max - chunk.dirty_rect_min
			draw_rect(Rect2i((chunk.dirty_rect_min) * pixel_scale, size * pixel_scale + centering_val), Color(0.85, 0.317, 0.008, 1.0), false, 1)

##Used to call a single update tick to the simulation. For a constantly running simulation, putting in [method _process] is recommended.
func update_simulation() -> void:
	
	accumulator += get_process_delta_time() * simulation_speed
	
	if accumulator >= sim_dt:
		if use_checkerboard_updates:
			if ticks_passed == 0:
				update_chunks(true)
			else:
				update_chunks(false)
		else:
			update_chunks()
		
		render_chunk_updates()
		
		ticks_passed += 1
		ticks_passed %= 2
		accumulator -= sim_dt
		
	
