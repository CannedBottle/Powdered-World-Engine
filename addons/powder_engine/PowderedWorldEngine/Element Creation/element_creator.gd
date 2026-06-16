@tool
extends VBoxContainer



func _ready() -> void:
	$test/TypeSelection.pressed.connect(_on_pressed)


func _on_pressed():
	ElementEnumGenerator.GenerateElementEnum()
