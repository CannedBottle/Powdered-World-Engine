@tool
extends VBoxContainer
@onready var apply_changes_button: Button = $Apply/ApplyChangesButton



func _ready() -> void:
	apply_changes_button.pressed.connect(apply_changes)


func apply_changes():
	ElementEnumGenerator.GenerateElementAttributes()
