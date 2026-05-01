extends Node

enum Elements{
	AIR,
	SAND,
	WATER,
	ACID,
	STONE,
	WALL
}

## a way of determining what special attributes an element has.
enum ElementTypes{
	STATIC,
	SOLID,
	LIQUID,
	GAS
}

const element_to_type: Dictionary[Elements, ElementTypes] = {
	Elements.AIR: ElementTypes.STATIC,
	Elements.SAND: ElementTypes.SOLID,
	Elements.WATER: ElementTypes.LIQUID,
	Elements.ACID: ElementTypes.LIQUID,
	Elements.STONE: ElementTypes.SOLID,
	Elements.WALL: ElementTypes.STATIC,
}

## in the random values, a [code]Vector2i[/code] is used for min/max values since constants cannot use random functions.
const element_type_defaults: Dictionary[ElementTypes, Dictionary] = {
	ElementTypes.STATIC: {"none": null},
	
	ElementTypes.SOLID: {"random": Vector2i(0, 1)},
	
	ElementTypes.LIQUID: {
		"random": Vector2i(0, 1),
		"direction": 0,
		"density": 0,
	},
	
	ElementTypes.GAS: {"random": Vector2i(0, 7)},
}

const base_element_colors: Dictionary[Elements, Vector4] = {
	Elements.AIR: Vector4.ZERO,
	Elements.SAND: Vector4(1.0, 0.93, 0.474, 1.0),
	Elements.WATER: Vector4(0.112, 0.644, 0.93, 0.6),
	Elements.ACID: Vector4(0.678, 1.0, 0.31, 0.75),
	Elements.STONE: Vector4(0.58, 0.58, 0.58, 1.0),
	Elements.WALL: Vector4(0.27, 0.27, 0.27, 1.0)
}

# // --------------------- USEFUL FUNCTIONS ----------------------------------- //
func world_pos_to_chunkidx(worldpos: Vector2i, chunksize: int, inverse_chunksize: float) -> int:
	@warning_ignore("integer_division")
	var chunk_pos: Vector2i = worldpos * inverse_chunksize
	var local_x: int = worldpos.x - (chunk_pos.x * chunksize)
	var local_y: int = worldpos.y - (chunk_pos.y * chunksize)
	return local_y * chunksize + local_x

# // --------------------- ELEMENT MOVEMENT RULESETS ---------------------------- //

func on_update(cell: Cell):
	if cell.type == ElementTypes.LIQUID:
		if cell.try_move("bottommiddle") == false:
					var success: bool = false
					if cell.random == 0:
						success = cell.try_move("bottomright")
						if success == false:
							success = cell.try_move("bottomleft")
					else:
						success = cell.try_move("bottomleft")
						if success == false:
							success = cell.try_move("bottomright")
					
					if success == false:
						if cell.type_attributes["direction"] == 1:
							success = cell.try_move("rightmiddle")
							cell.type_attributes["direction"] = int(success)
							
							if success == false:
								cell.try_move("leftmiddle")
						else:
							success = cell.try_move("leftmiddle")
							cell.type_attributes["direction"] = 0 if success else 1
							
							if success == false:
								cell.try_move("rightmiddle")
	else:
		match cell.element:
			Elements.SAND:
				if cell.try_move("bottommiddle") == false:
					if cell.random == 0:
						if cell.try_move("bottomright") == false:
							cell.try_move("bottomleft")
					else:
						if cell.try_move("bottomleft") == false:
							cell.try_move("bottomright")


# // ------------------------- CELL/CHUNK CLASSES ---------------------------- //

class Cell:
	var position: Vector2i
	var type: ElementTypes
	var element: Elements:
		set(value):
			element = value
			type = element_to_type[value]
			
			if type == ElementTypes.LIQUID:
				brightness = 1.0
			else:
				brightness = randf_range(0.9, 1.0)
			
			replace_type_attributes_with_defaults(type)
	
	var type_attributes: Dictionary
	
	var chunk_size: int
	var neighbors: Dictionary[String, Vector2i] # if neighbor is an edge, will show up as (-1, -1)
	var neighbor_strings: Array[String] = ["topleft", "topmiddle", "topright", "leftmiddle", "rightmiddle", "bottomleft", "bottommiddle", "bottomright"]
	var sim_ref: PowderSimulation
	var random: int = randi_range(0, 1)
	var brightness: float
	var chunk: Chunk
	var sim_size: Vector2i
	#idx of cell inside the chunk's own cells list, basically local position
	var chunk_idx: int
	
	#if on the edge of a chunk, so can update using both chunks
	var chunk_edge: bool = false
	var sim_edge: bool = false
	
	
	@warning_ignore("shadowed_variable")
	func _init(position: Vector2i, chunk_size: int, type: Elements, sim_ref: PowderSimulation, chunk: Chunk, chunk_idx: int) -> void:
		self.position = position
		self.element = type
		self.chunk_size = chunk_size
		self.sim_ref = sim_ref
		self.chunk = chunk
		self.chunk_idx = chunk_idx
		
		self.sim_size = sim_ref.simulation_size
		
		
		# if the cell is at the edge of the chunk or not
		self.chunk_edge = position.x == chunk.max_extents.x or position.y == chunk.max_extents.y or position.x == chunk.min_extents.x or position.y == chunk.min_extents.y
		
		self.sim_edge = (position.x == 0 or position.y == 0 or position.x == sim_size.x or position.y == sim_size.y)
	
	
	func replace_type_attributes_with_defaults(new_type: ElementTypes):
		type_attributes = element_type_defaults[new_type].duplicate()
		
		if !type_attributes.get("random") == null:
			type_attributes["random"] = randi_range(type_attributes["random"].x, type_attributes["random"].y)
		
		if !type_attributes.get("direction") == null:
			type_attributes["direction"] = type_attributes["random"]
	
	func find_neighbor_indices():
		#loop through neighbor spots, determine if edge, if so set to (0, 0), if not set to position on tilemap
		var loopnum: int = 0
		for y in [-1, 0, 1]:
			for x in [-1, 0, 1]:
				if x == 0 and y == 0:
					continue
				neighbors.get_or_add(neighbor_strings[loopnum], Vector2i(self.position.x + x, self.position.y + y))
				
				#edge checking
				if(self.position.x + x > sim_size.x or self.position.x + x < 0 or
				self.position.y + y > sim_size.y or self.position.y + y < 0):
					neighbors[neighbor_strings[loopnum]] = Vector2i(-1, -1)
				
				loopnum += 1
	
		
	
	# // ------------------------ Movement 'n shi(im going insane) ------------------------------ //
	func can_move(neighbor_cell: Cell) -> bool:
		var valid_move: bool = false
		match type:
			ElementTypes.SOLID:
				valid_move = neighbor_cell.type == ElementTypes.LIQUID or neighbor_cell.type == ElementTypes.GAS
		
		return valid_move
	
	##returns whether the move was successful or not.
	func try_move(to_neighbor: String) -> bool:
		var neighbor_pos: Vector2i = neighbors[to_neighbor]
		if neighbor_pos == Vector2i(-1, -1):
			return false
		
		var write_chunk: Chunk = chunk
		
		if chunk_edge:
			@warning_ignore("integer_division")
			var new_chunk_pos: Vector2i = neighbor_pos * chunk.inv_chunksize
			
			write_chunk = sim_ref.chunks[new_chunk_pos]
			
		
		var neighbor_idx: int = SandInfo.world_pos_to_chunkidx(neighbor_pos, chunk_size, chunk.inv_chunksize)
		
		#valid cell to move checking (per type check)
		var valid_move: bool = false
		var neighbor_cell: Cell = write_chunk.cells[neighbor_idx]
		if neighbor_cell.element == Elements.AIR:
			valid_move = true
		else:
			valid_move = can_move(neighbor_cell)
		
		if valid_move == true:
			
			chunk.mark_cell_updated(self)
			write_chunk.mark_cell_updated(neighbor_cell)
			
			
			#if on edge of chunk and successfully updates, wake chunk next to it
			if chunk_edge and not sim_edge:
				#wake the chunk nearest to cell
				var pos_addition: Vector2i = Vector2i(
					int(position.x == chunk.max_extents.x) - int(position.x == chunk.min_extents.x),
					int(position.y == chunk.max_extents.y) - int(position.y == chunk.min_extents.y)
				)
				if not pos_addition == Vector2i.ZERO:
					sim_ref.chunks[chunk.chunk_position + pos_addition].wake()
			
			
			chunk.swap_cells(self, neighbor_cell)
			write_chunk.wake()
			
			return true
		else:
			return false

class Chunk:
	var chunk_size: int = 64
	#position of chunk in the grid of chunks (not cells)
	var chunk_position: Vector2i = Vector2i.ZERO
	#reference to simulation node
	var sim_ref: PowderSimulation
	#how many updates of no change before sleeping
	var insomnia: int = 1
	#keeps track of updates of no change for insomnia
	var insomnia_count: int = 0
	
	var cells: Array[Cell]
	#cells to draw, also cells that have been updated in the past frame
	var updated_cells_mask: Array[bool]
	var updated_cells_indexes: Array[int]
	
	#the max/min values the chunk cells dict contains, used for moving cells from one chunk to another
	var max_extents: Vector2i
	var min_extents: Vector2i
	
	#Dirty rect extents
	var dirty_rect_max: Vector2i
	var dirty_rect_min: Vector2i
	var has_valid_dirty_rect: bool = false
	
	#to remove division operations
	var inv_chunksize: float
	
	var sleeping: bool = false
	var renderer: chunk_renderer = null
	
	@warning_ignore("shadowed_variable")
	func _init(chunk_size: int, chunk_position: Vector2i, sim_ref: PowderSimulation, insomnia: int) -> void:
		self.chunk_size = chunk_size
		self.chunk_position = chunk_position
		self.sim_ref = sim_ref
		self.insomnia = insomnia
		
		self.inv_chunksize = 1.0 / float(chunk_size)
		self.updated_cells_mask.resize(chunk_size * chunk_size)
		
		# set extents
		self.max_extents = Vector2i(
			chunk_position.x * chunk_size + chunk_size - 1,
			chunk_position.y * chunk_size + chunk_size - 1,
		)
		self.min_extents = Vector2i(
			chunk_position.x * chunk_size,
			chunk_position.y * chunk_size
		)
		self.dirty_rect_max = max_extents
		self.dirty_rect_min = min_extents
		
		init_cells()
	
	
	func init_cells():
		var idx: int = 0
		for y in chunk_size:
			for x in chunk_size:
				# find position in all of the cells, not just chunk
				var pos: Vector2i = Vector2i(
					x + (chunk_position.x * chunk_size),
					y + (chunk_position.y * chunk_size)
				)
				
				cells.append(Cell.new(pos, self.chunk_size, Elements.AIR, self.sim_ref, self, idx))
				
				idx += 1
		
		for cell in cells:
			cell.find_neighbor_indices()
	
	
	func update_half_cells(invert_checkerboard: bool = false, use_checkerboard_updates: bool = true):
		if self.sleeping:
			if renderer.show_debug_info:
				renderer.debug_info.modulate = Color(0.24, 0.24, 0.24, 1.0)
			return
		elif renderer.show_debug_info:
			renderer.debug_info.modulate = Color(1.0, 1.0, 1.0, 1.0)
		
		var index: int = 0
		for cell in cells:
			if use_checkerboard_updates:
				index += 1
				if index % chunk_size == 0:
					invert_checkerboard = !invert_checkerboard
				if invert_checkerboard:
					if index % 2 == 0:
						continue
				else:
					if index % 2 == 1:
						continue
			
			
			if has_valid_dirty_rect:
				if not (cell.position <= dirty_rect_max + Vector2i(2, 2) and cell.position >= dirty_rect_min - Vector2i(2, 2)):
					
					#inflate over chunk borders
					continue
			
			if updated_cells_mask[cell.chunk_idx] == true or cell.element == Elements.AIR:
				continue
			
			SandInfo.on_update(cell)
		#sleeps cell if no cells are updated
		if updated_cells_indexes.size() == 0:
			if insomnia_count >= insomnia - 1:
				insomnia_count = 0
				sleep()
			else:
				insomnia_count += 1
		else:
			insomnia_count = 0
	
	func mark_cell_updated(cell: Cell):
		updated_cells_indexes.append(cell.chunk_idx)
		updated_cells_mask[cell.chunk_idx] = true
	
	#TODO: WHEN MAKING INTO C#, DO NOT USE THIS AND FOR THE MASK USE A FIXED-SIZE BOOL LIST WITH .Clear()
	func clear_updated_mask():
		for idx in updated_cells_mask.size():
			updated_cells_mask[idx] = false
	
	func update_dirty_rect():
		has_valid_dirty_rect = false
		self.dirty_rect_max = self.min_extents
		self.dirty_rect_min = self.max_extents
		
		for idx in updated_cells_indexes:
			var pos: Vector2i = cells[idx].position
			dirty_rect_max.x = max(dirty_rect_max.x, pos.x)
			dirty_rect_max.y = max(dirty_rect_max.y, pos.y)
			
			dirty_rect_min.x = min(dirty_rect_min.x, pos.x)
			dirty_rect_min.y = min(dirty_rect_min.y, pos.y)
		
		if dirty_rect_max != self.min_extents and dirty_rect_min != self.max_extents:
			has_valid_dirty_rect = true
	
	func send_draw_info_to_renderer():
		if self.updated_cells_indexes.size() == 0:
			return
		
		for idx in updated_cells_indexes:
			var cell: Cell = cells[idx]
			#base element color
			var col: Vector4 = SandInfo.base_element_colors[cell.element]
			#darkness of pixel
			var d: float = 1.0 - cell.brightness
			#darkness applied only on non-alpha channels
			renderer.cell_values[cell.chunk_idx] = Vector4(col.x - d, col.y - d, col.z - d, col.w)
		self.clear_updated_mask()
		self.updated_cells_indexes.clear()
		renderer.cells_updated.emit()
	
	## a function to put all the necessary variables to carry over when moving cells
	func swap_cells(copy_cell: Cell, paste_cell: Cell):
		var paste_cell_element: Elements = paste_cell.element
		var paste_cell_random: int = paste_cell.random
		var paste_cell_type_attributes: Dictionary = paste_cell.type_attributes
		
		paste_cell.element = copy_cell.element
		paste_cell.random = copy_cell.random
		paste_cell.type_attributes = copy_cell.type_attributes
		
		copy_cell.element = paste_cell_element
		copy_cell.random = paste_cell_random
		copy_cell.type_attributes = paste_cell_type_attributes
	
	func sleep():
		self.sleeping = true
	
	func wake():
		self.sleeping = false
	
