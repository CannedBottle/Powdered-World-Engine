@tool
extends VBoxContainer
@onready var apply_changes_button: Button = $Apply/ApplyChangesButton
@onready var element_selection: OptionButton = $Element/ElementSelection
@onready var name_field: LineEdit = $Name/NameField

# ----------------------------------------------------------- Danger Zone
@onready var delete_element_button: Button = $DangerSection/VBoxContainer/DeleteElement/DeleteElementButton
@onready var reset_defaults_button: Button = $DangerSection/VBoxContainer/ResetDefaults/ResetDefaultsButton


const add_icon := preload("res://addons/powder_engine/PowderedWorldEngine/Assets/Add.svg")

var temp_element_names: Array[StringName]

var E_storage_ref: ElementStorage

var temp_attributes: Dictionary[StringName, SandInfoCS.ElementAttributes]

var selected_element: StringName

func _ready() -> void:
	E_storage_ref = SandInfoCS.GetElementResource()
	set_temp_to_actual()
	update_element_selector()
	update_button_states()
	
	
	# ------------- Connections -------------- #
	apply_changes_button.pressed.connect(apply_changes)
	element_selection.item_selected.connect(_new_selected_element)
	name_field.text_submitted.connect(_new_element_name)
	
	reset_defaults_button.pressed.connect(reset_defaults)

func update_element_selector(keep_idx: bool = false):
	# --------- Element Selection ------------- #
	var idx: int = element_selection.selected
	
	element_selection.clear()
	for name in temp_element_names:
		element_selection.add_item(name)
	element_selection.add_item("Add Element")
	element_selection.set_item_icon(-1, add_icon)
	
	if keep_idx:
		element_selection.select(idx)
	# ---------------------------------------- #

func update_button_states():
	# ------------- Element Name -------------- #
	name_field.text = get_selected_element()
	# ------------- Element Selector -------------- #
	if element_selection.item_count > temp_element_names.size() + 1:
		element_selection.remove_item(element_selection.item_count)

func set_temp_to_actual():
	if E_storage_ref.AllElementAttributes.size() > 0:
		temp_element_names = E_storage_ref.AllElementAttributes.keys()
	
	temp_attributes = E_storage_ref.AllElementAttributes.duplicate_deep()

func set_actual_to_temp():
	SandInfoCS.ClearElementNames()
	for tname in temp_element_names:
		SandInfoCS.AddElementName(tname)
	
	E_storage_ref.AllElementAttributes = temp_attributes.duplicate_deep()

func reset_defaults():
	E_storage_ref.ResetDefaults()

func apply_changes():
	ElementEnumGenerator.GenerateElementAttributes()

func get_selected_element() -> String:
	return temp_element_names[element_selection.selected]

func add_placeholder_element():
	var count: int = 0
	for name in temp_element_names:
		if name.contains("ELEMENT"):
			count += 1
	
	temp_element_names.append("ELEMENT" + str(count))
	temp_attributes.get_or_add(StringName("ELEMENT" + str(count)))

# -------------------- Signals ------------------- #
func _new_selected_element(index: int):
	if index == temp_element_names.size():
		add_placeholder_element()
		
		update_element_selector()
		element_selection.selected = index
	
	update_button_states()
	

func _new_element_name(new_text: String):
	temp_element_names[element_selection.selected] = new_text
	update_element_selector(true)
