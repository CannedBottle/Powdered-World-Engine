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
	Elements.WATER: Vector4(0.112, 0.644, 0.93, 0.75),
	Elements.ACID: Vector4(0.678, 1.0, 0.31, 0.75),
	Elements.STONE: Vector4(0.58, 0.58, 0.58, 1.0),
	Elements.WALL: Vector4(0.27, 0.27, 0.27, 1.0)
}

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
			replace_type_attributes_with_defaults(type)
	
	var type_attributes: Dictionary
	
	var chunk_size: int
	var neighbors: Dictionary[String, Vector2i] # if neighbor is an edge, will show up as (0, 0)
	var neighbor_strings: Array[String] = ["topleft", "topmiddle", "topright", "leftmiddle", "rightmiddle", "bottomleft", "bottommiddle", "bottomright"]
	var sim_ref: PowderSimulation
	var updated: bool = false
	var random: int = randi_range(0, 1)
	var brightness: float = randf_range(0.9, 1.0)
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
	func can_move(to_neighbor: String, write_chunk: Chunk = null) -> bool:
		var neighbor_pos: Vector2i = neighbors[to_neighbor]
		
		if write_chunk == null:
			#find write chunk
			write_chunk = chunk
			if chunk_edge and not neighbor_pos == Vector2i(-1, -1):
				@warning_ignore("integer_division")
				var new_chunk_pos: Vector2i = neighbor_pos / chunk.chunk_size
				
				write_chunk = sim_ref.chunks[new_chunk_pos]
		
		if neighbor_pos == Vector2i(-1, -1):
			return false
		#valid cell to move checking
		var valid_move: bool = false
		var neighbor_cell: Cell = write_chunk.cells[neighbor_pos]
		if neighbor_cell.element == Elements.AIR:
			valid_move = true
		else:
			match type:
				ElementTypes.SOLID:
					valid_move = neighbor_cell.type == ElementTypes.LIQUID or neighbor_cell.type == ElementTypes.GAS
		return valid_move
	
	##returns whether the move was successful or not.
	func try_move(to_neighbor: String) -> bool:
		var write_chunk: Chunk = chunk
		
		var neighbor_pos: Vector2i = neighbors[to_neighbor]
		if chunk_edge and not neighbor_pos == Vector2i(-1, -1):
			@warning_ignore("integer_division")
			var new_chunk_pos: Vector2i = neighbor_pos / chunk.chunk_size
			
			write_chunk = sim_ref.chunks[new_chunk_pos]
			
		
		var valid_move = can_move(to_neighbor, write_chunk)
		
		if valid_move == true:
			var neighbor_cell: Cell = write_chunk.cells[neighbor_pos]
			
			chunk.updated_cells.get_or_add(position, self)
			write_chunk.updated_cells.get_or_add(neighbor_cell.position, neighbor_cell)
			
			
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
	
	var cells: Dictionary[Vector2i, Cell]
	#cells to draw, also cells that have been updated in the past frame
	var updated_cells: Dictionary[Vector2i, Cell]
	
	#the max/min values the chunk cells dict contains, used for moving cells from one chunk to another
	var max_extents: Vector2i
	var min_extents: Vector2i
	
	#Dirty rect extents
	var dirty_rect_max: Vector2i
	var dirty_rect_min: Vector2i
	var has_valid_dirty_rect: bool = false
	
	var sleeping: bool = false
	var renderer: chunk_renderer = null
	
	@warning_ignore("shadowed_variable")
	func _init(chunk_size: int, chunk_position: Vector2i, sim_ref: PowderSimulation, insomnia: int) -> void:
		self.chunk_size = chunk_size
		self.chunk_position = chunk_position
		self.sim_ref = sim_ref
		self.insomnia = insomnia
		
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
				
				cells.get_or_add(pos, Cell.new(pos, self.chunk_size, Elements.AIR, self.sim_ref, self, idx))
				
				idx += 1
		
		for cell in self.cells.values():
			cell.find_neighbor_indices()
	
	
	func update_half_cells(invert_checkerboard: bool = false, use_checkerboard_updates: bool = true):
		if self.sleeping:
			if renderer.show_debug_info:
				renderer.debug_info.modulate = Color(0.24, 0.24, 0.24, 1.0)
			return
		elif renderer.show_debug_info:
			renderer.debug_info.modulate = Color(1.0, 1.0, 1.0, 1.0)
		
		var index: int = 0
		for cell in self.cells.values():
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
			
			if cell.position in updated_cells or cell.element == Elements.AIR:
				continue
			
			SandInfo.on_update(cell)
		#sleeps cell if no cells are updated
		if updated_cells.size() == 0:
			if insomnia_count >= insomnia - 1:
				insomnia_count = 0
				sleep()
			else:
				insomnia_count += 1
		else:
			insomnia_count = 0
	
	func update_dirty_rect():
		has_valid_dirty_rect = false
		self.dirty_rect_max = self.min_extents
		self.dirty_rect_min = self.max_extents
		
		for pos in updated_cells.keys():
			dirty_rect_max.x = max(dirty_rect_max.x, pos.x)
			dirty_rect_max.y = max(dirty_rect_max.y, pos.y)
			
			dirty_rect_min.x = min(dirty_rect_min.x, pos.x)
			dirty_rect_min.y = min(dirty_rect_min.y, pos.y)
		
		if dirty_rect_max != self.min_extents and dirty_rect_min != self.max_extents:
			has_valid_dirty_rect = true
	
	func send_draw_info_to_renderer():
		if self.updated_cells.size() == 0:
			return
		
		for cell in self.updated_cells.values():
			#base element color
			var col: Vector4 = SandInfo.base_element_colors[cell.element]
			#darkness of pixel
			var d: float = 1.0 - cell.brightness
			#darkness applied only on non-alpha channels
			renderer.cell_values[cell.chunk_idx] = Vector4(col.x - d, col.y - d, col.z - d, col.w)
		self.updated_cells.clear()
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
	
