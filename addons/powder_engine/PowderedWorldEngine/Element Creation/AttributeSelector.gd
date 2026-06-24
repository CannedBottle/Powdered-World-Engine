extends HBoxContainer
class_name AttributeSelector


var options: Array[String]
var selected_index: int = 0
#the index of the attribute in the original array of attributes
var this_index: int = 0

var option_picker: OptionButton

const trash_icon_path: String = "res://addons/powder_engine/PowderedWorldEngine/Assets/Remove.svg"

signal attribute_updated(attribute_index: int, new_selected_index: int)

func _ready() -> void:
	alignment = BoxContainer.ALIGNMENT_CENTER

func create_children():
	# --------- LABEL -------------
	var new_flag_label: Label = Label.new()
	new_flag_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	new_flag_label.text = "Flag " + str(this_index)
	add_child(new_flag_label)
	
	# -------- OptionButton --------
	option_picker = OptionButton.new()
	for attribute in options:
		option_picker.add_item(attribute)
	option_picker.select(selected_index)
	option_picker.search_bar_enabled = true
	add_child(option_picker)
	
	option_picker.item_selected.connect(_on_flag_selected)
	
	# ------- TrashButton ----------
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
	queue_free()
