@tool
extends HBoxContainer

const unmarked_icon_path: String = "res://addons/powder_engine/PowderedWorldEngine/Assets/toggleOff.png"
const marked_icon_path: String = "res://addons/powder_engine/PowderedWorldEngine/Assets/toggleOn.png"

@onready var reaction_editor: FoldableContainer = $ReactionEditor
@onready var remove_button: Button = $Remove

@onready var neighbor_button_parent: GridContainer = $ReactionEditor/options/MarkedNeighbors/NeighborButtonParent

@onready var comparison_options: OptionButton = $ReactionEditor/options/HBoxContainer/ComparisonOptions
@onready var compare_amount: SpinBox = $ReactionEditor/options/HBoxContainer/compareAmount
@onready var condition_element_picker: OptionButton = $ReactionEditor/options/HBoxContainer/ElementPicker

@onready var react_type_options: OptionButton = $ReactionEditor/options/ReactOptions

@onready var effect_options_e_picker: OptionButton = $ReactionEditor/options/Element/EffectOpsElementPicker


var element_creator: Control

signal removed(reaction: int)
# unused
signal option_changed(reaction: int)

##The same as the index in the ElementAttributes instance pointing to the attached reaction object
var reaction_num: int
var this_attribute: ElementAttributes

var marked_neighbors: Dictionary[String, bool] = {
	"topleft": false,
	"topmiddle": false,
	"topright": false,
	"leftmiddle": false,
	"rightmiddle": false,
	"bottomleft": false,
	"bottommiddle": false,
	"bottomright": false
}

const neighbor_pos_to_name: Dictionary[Vector2i, String] = {
	Vector2i(-1, -1): "topleft",
	Vector2i(0, -1): "topmiddle",
	Vector2i(1, -1): "topright",
	Vector2i(-1, 0): "leftmiddle",
	Vector2i(1, 0): "rightmiddle",
	Vector2i(-1, 1): "bottomleft",
	Vector2i(0, 1): "bottommiddle",
	Vector2i(1, 1): "bottomright"
}



func _ready() -> void:
	
	# connect toggle signals 
	for btn: Button in neighbor_button_parent.get_children():
		if(btn.name == "middle"): 
			btn.text = "E"
			btn.icon = null
			continue
		
		btn.toggled.connect(_on_neighbor_toggled.bind(btn))
		btn.button_pressed = false
		apply_icon_state(btn)
	
	
	# connect signals -------------------
	remove_button.pressed.connect(_on_remove)
	
	comparison_options.item_selected.connect(_on_comparison_selected)
	compare_amount.value_changed.connect(_on_compare_amount_changed)
	condition_element_picker.item_selected.connect(_on_element_check_selected)
	
	react_type_options.item_selected.connect(_on_react_options_selected)
	effect_options_e_picker.item_selected.connect(_on_effect_ops_e_picker_selected)

## use this function after instantiating the scene in the main ui scene
func setup(assigned_idx: int, elem_creator: Control):
	reaction_num = assigned_idx
	reaction_editor.title = "Reaction " + str(assigned_idx + 1)
	reaction_editor.folded = false
	
	element_creator = elem_creator
	
	this_attribute = element_creator.temp_attributes[element_creator.selected_element]
	
	fill_element_pickers()
	
	update_to_match()

func fill_element_pickers():
	element_creator.fill_optionbutton_with_elements(condition_element_picker, true, false)
	element_creator.fill_optionbutton_with_elements(effect_options_e_picker, false, false)

##updates all children to match the stored ElementAttributes values.
func update_to_match():
	update_neighbor_buttons(this_attribute.GetReactionMarkedNeighbors(reaction_num))
	
	comparison_options.select(this_attribute.GetReactionNeighborElementComparison(reaction_num))
	compare_amount.value = this_attribute.GetReactionMatchingNumCheck(reaction_num)
	condition_element_picker.select(this_attribute.GetReactionElementCheck(reaction_num))
	react_type_options.select(this_attribute.GetReactionType(reaction_num))
	effect_options_e_picker.select(this_attribute.GetReactionReplaceElement(reaction_num))

func update_neighbor_buttons(with: Array[Vector2i]):
	for pos: Vector2i in neighbor_pos_to_name.keys():
		if with.has(pos):
			marked_neighbors[neighbor_pos_to_name[pos]] = true
		else:
			marked_neighbors[neighbor_pos_to_name[pos]] = false
	
	upd_neighbor_buttons_todict()

## updates the marked neighbor buttons to match the marked_neighbors dict
func upd_neighbor_buttons_todict():
	for child: Button in neighbor_button_parent.get_children():
		if(child.name == "middle"): continue
		
		child.button_pressed = marked_neighbors[child.name]

func apply_icon_state(to_button: Button):
	if(to_button.button_pressed):
		to_button.icon = load(marked_icon_path)
	else:
		to_button.icon = load(unmarked_icon_path)

func set_attribute_markedneighbors():
	# get array of positions
	var checked_positions: Array[Vector2i] = []
	for neighbor_name: String in marked_neighbors.keys():
		if(marked_neighbors[neighbor_name] == true):
			checked_positions.append(neighbor_pos_to_name.find_key(neighbor_name))
	
	# set the array
	this_attribute.SetReactionMarkedNeighbors(reaction_num, checked_positions)
	

# -------------------- SIGNALS -------------------- #

func _on_remove():
	removed.emit(reaction_num)
	queue_free()

func _on_neighbor_toggled(on: bool, button: Button) -> void:
	marked_neighbors[button.name] = on
	apply_icon_state(button)
	option_changed.emit(reaction_num)
	
	set_attribute_markedneighbors()

# button signals

func _on_comparison_selected(index: int):
	this_attribute.SetReactionNeighborElementComparison(reaction_num, index)

func _on_compare_amount_changed(value: float):
	this_attribute.SetReactionMatchingNumCheck(reaction_num, value)

func _on_element_check_selected(index: int):
	var index_to_store: int = index
	if(condition_element_picker.get_item_text(index) == "Any"): index_to_store = -1
	
	this_attribute.SetReactionElementCheck(reaction_num, index_to_store)
	
	option_changed.emit(reaction_num)

func _on_react_options_selected(index: int):
	this_attribute.SetReactionType(reaction_num, index)

func _on_effect_ops_e_picker_selected(index: int):
	this_attribute.SetReactionReplaceElement(reaction_num, index)
