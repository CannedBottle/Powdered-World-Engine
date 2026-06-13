shader_type canvas_item;

uniform sampler2D cell_tex : filter_nearest;

void fragment() {
	COLOR = texture(cell_tex, UV);
	
}
