shader_type canvas_item;

//const int chunk_size = 64;

uniform bool use_texture_data = true;

uniform sampler2D cell_tex : filter_nearest;

//uniform vec4 cells[chunk_size * chunk_size];
uniform vec4 cells[1];

void fragment() {
	
	if(use_texture_data)
	{
		COLOR = texture(cell_tex, UV);
	}
	else
	{
		vec2 res = 1.0 / TEXTURE_PIXEL_SIZE;
		vec2 pixel_uv = floor(UV * res);
		
		float idx;
		
		idx = pixel_uv.x + pixel_uv.y * res.x;
		
		COLOR = cells[int(round(idx))];
	}
	
}

//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
