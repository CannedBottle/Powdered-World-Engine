@tool
extends VBoxContainer
@onready var apply_changes_button: Button = $Apply/ApplyChangesButton
@onready var element_selection: OptionButton = $Element/ElementSelection
@onready var name_field: LineEdit = $Name/NameField
@onready var type_selection: OptionButton = $Type/TypeSelection

# ----------------------------------------------------------- Visuals
@onready var color_selection: ColorPickerButton = $VisualSection/VBoxContainer/Color/ColorSelection
@onready var noise_strength_field: SpinBox = $VisualSection/VBoxContainer/NoiseStrength/NoiseStrengthField

# ----------------------------------------------------------- Flags
@onready var flags_parent: VBoxContainer = $FlagSection/FlagsParent
@onready var add_flag_button: Button = $FlagSection/FlagsParent/AddFlagButton

# ----------------------------------------------------------- Danger Zone
@onready var delete_element_button: Button = $DangerSection/VBoxContainer/DeleteElement/DeleteElementButton
@onready var reset_defaults_button: Button = $DangerSection/VBoxContainer/ResetDefaults/ResetDefaultsButton


const add_icon := preload("res://addons/powder_engine/PowderedWorldEngine/Assets/Add.svg")

var temp_element_names: Array[StringName]

var E_storage_ref: ElementStorage

var temp_attributes: Dictionary[StringName, ElementAttributes]

var selected_element: StringName

func _ready() -> void:
	E_storage_ref = SandInfoCS.GetElementResource()
	
	
	set_temp_to_actual()
	update_element_selector()
	update_button_states()
	
	#fill element type selector
	type_selection.clear()
	for type in SandInfoCS.GetElementTypes():
		type_selection.add_item(type)
	
	#test flag area creation
	add_flag()
	
	# ------------- Connections -------------- #
	apply_changes_button.pressed.connect(apply_changes)
	element_selection.item_selected.connect(_new_selected_element)
	name_field.text_submitted.connect(_new_element_name)
	type_selection.item_selected.connect(_type_changed)
	
	color_selection.popup_closed.connect(_color_changed)
	noise_strength_field.value_changed.connect(_noise_strength_changed)
	
	add_flag_button.pressed.connect(add_flag)
	
	reset_defaults_button.pressed.connect(_reset_defaults)
	delete_element_button.pressed.connect(_delete_selected_element)

func update_element_selector(keep_idx: bool = false):
	# --------- Element Selection ------------- #
	var idx: int = element_selection.selected
	
	element_selection.clear()
	var i: int = 0
	for name in temp_element_names:
		element_selection.add_item(name)
		var new_icon: Image = Image.create_empty(15, 15, false, Image.Format.FORMAT_RGBA8)
		new_icon.fill(temp_attributes[name].BaseColor)
		element_selection.set_item_icon(i, ImageTexture.create_from_image(new_icon))
		
		i += 1
	element_selection.add_item("Add Element")
	element_selection.set_item_icon(-1, add_icon)
	
	if keep_idx:
		element_selection.select(idx)
	else:
		element_selection.select(0)
	# ---------------------------------------- #

func update_button_states():
	var selected_element: StringName = get_selected_element()
	
	# ------------- Element Name -------------- #
	name_field.text = selected_element
	# ------------- Element Selector -------------- #
	if element_selection.item_count > temp_element_names.size() + 1:
		element_selection.remove_item(element_selection.item_count)
	
	type_selection.select(int(temp_attributes[selected_element].Type))
	
	# ------------- Visuals --------------------- #
	color_selection.color = temp_attributes[selected_element].BaseColor
	noise_strength_field.value = temp_attributes[selected_element].NoiseStrength

func set_temp_to_actual():
	if E_storage_ref.AllElementAttributes.size() > 0:
		temp_element_names = E_storage_ref.AllElementAttributes.keys()
		
		temp_attributes = E_storage_ref.AllElementAttributes.duplicate(true)
	
	set_temp_attribute_names()

func set_actual_to_temp():
	E_storage_ref.AllElementAttributes = temp_attributes.duplicate_deep()

func _reset_defaults():
	
	E_storage_ref.ResetDefaults()
	set_temp_attribute_names()
	
	set_temp_to_actual()
	update_element_selector()
	update_button_states()

func apply_changes():
	set_actual_to_temp()
	SandInfoCS.SaveElementStorage()
	ElementEnumGenerator.GenerateElementAttributes()

func get_selected_element() -> StringName:
	if temp_element_names.size() > 0:
		return temp_element_names[element_selection.selected]
	else:
		return &"None"

func set_temp_attribute_names():
	for ename in temp_element_names:
		temp_attributes[ename].StringName = ename;

func add_placeholder_element():
	var count: int = 0
	for name in temp_element_names:
		if name.contains("ELEMENT"):
			count += 1
	
	temp_element_names.append("ELEMENT" + str(count))
	temp_attributes.get_or_add(StringName("ELEMENT" + str(count)), ElementStorage.GetDefaultElement())

func create_flag_selector():
	var new_attributes: AttributeSelector = AttributeSelector.new()
	new_attributes.this_index = temp_attributes[get_selected_element()].Flags.size()
	new_attributes.options = SandInfoCS.GetElementFlagsAsString()
	flags_parent.add_child(new_attributes)
	new_attributes.create_children()
	flags_parent.move_child(add_flag_button, -1)

func add_flag():
	create_flag_selector()
	#make code for adding an actual flag here

# -------------------- Signals ------------------- #
func _new_selected_element(index: int):
	if index == temp_element_names.size():
		add_placeholder_element()
		
		update_element_selector()
		element_selection.selected = index
	
	update_button_states()
	

func _new_element_name(new_text: String):
	var old_name: StringName = get_selected_element()
	
	if ElementStorage.GetDefaultElementDict().has(old_name):
		name_field.text = old_name
		return
	
	var attributes: ElementAttributes = temp_attributes[old_name]
	temp_element_names[element_selection.selected] = new_text
	temp_attributes.erase(old_name)
	temp_attributes.get_or_add(new_text, attributes)
	
	update_element_selector(true)

func _color_changed():
	
	temp_attributes[get_selected_element()].BaseColor = color_selection.color
	
	update_button_states()
	update_element_selector(true)

func _noise_strength_changed(new_val: float):
	
	temp_attributes[get_selected_element()].NoiseStrength = new_val
	
	

func _delete_selected_element():
	var selected_element_name: StringName = get_selected_element()
	if ElementStorage.GetDefaultElementDict().has(selected_element_name):
		return
	
	temp_attributes.erase(selected_element_name)
	temp_element_names.erase(selected_element_name)
	
	update_element_selector()

func _type_changed(index: int):
	
	temp_attributes[get_selected_element()].Type = index

func _flag_updated(attribute_index: int, new_selected_index: int):
	pass
	#TODO: make code here for setting flags
