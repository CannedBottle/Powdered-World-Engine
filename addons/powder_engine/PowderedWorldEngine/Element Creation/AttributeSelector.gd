extends HBoxContainer
class_name AttributeSelector

enum Types{
	OPTION,
	NUMBER,
}

var type: Types = Types.OPTION

var options: Array[String]
var selected_index: int = 0
#the index of the attribute in the original array of attributes
var this_index: int = 0

var tooltip: String = ""

var option_picker: OptionButton

const trash_icon_path: String = "uid://f74vflvc1qcr"

signal attribute_updated(attribute_index: int, new_selected_index: int)

signal attribute_deleted(attribute_index: int)

func _ready() -> void:
	alignment = BoxContainer.ALIGNMENT_CENTER

func create_children(func_to_connect: Callable = Callable(), text: String = "Flag ", max_val: float = 255, rounded: bool = true, trash: bool = true):
	# --------- LABEL -------------
	var new_flag_label: Label = Label.new()
	new_flag_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	if(text == "Flag "):
		new_flag_label.text = text + str(this_index)
	else:
		new_flag_label.text = text
	new_flag_label.tooltip_text = tooltip
	new_flag_label.mouse_filter = Control.MOUSE_FILTER_PASS
	add_child(new_flag_label)
	
	# -------- OptionButton / SpinBox --------
	if(type == Types.OPTION): # init OptionButton
		option_picker = OptionButton.new()
		for attribute in options:
			option_picker.add_item(attribute)
		option_picker.select(selected_index)
		option_picker.search_bar_enabled = true
		add_child(option_picker)
		
		option_picker.item_selected.connect(_on_flag_selected)
	else: # init SpinBox
		var number_picker = SpinBox.new()
		number_picker.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		number_picker.max_value = max_val
		number_picker.rounded = rounded
		if(rounded == true):
			number_picker.step = 1.0
		else:
			number_picker.step = 0.01
		add_child(number_picker)
		number_picker.value_changed.connect(func_to_connect)
	
	# ------- TrashButton ----------
	if(trash):
		var new_trash_button: Button = Button.new()
		new_trash_button.icon = load(trash_icon_path)
		add_child(new_trash_button)
		new_trash_button.pressed.connect(_on_remove)
	

func get_selection_string() -> String:
	return options[selected_index]

# --------------- SIGNALS ----------------------------------------------------
func _on_flag_selected(index: int):
	selected_index = index
	attribute_updated.emit(this_index, selected_index)

func _on_remove():
	attribute_deleted.emit(this_index)
	queue_free()
