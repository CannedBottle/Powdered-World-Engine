using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class ChunkRendererCS : TextureRect
{

	[Export] public int ChunkSize = 30;
	[Export] public int PixelScale = 10;
	[Export] public bool ShowDebugInfo = false;


	private static readonly Shader RenderShader = GD.Load<Shader>("uid://dtywnulygwh48");

	ShaderMaterial Mat = new ShaderMaterial();

	public Image ChunkImage;
	public ImageTexture ChunkTexture;
	
	

	public override void _Ready()
	{


		// create an Image to assign to this textureRect with the dimensions needed for the shader
		Image Img = Image.CreateEmpty(ChunkSize, ChunkSize, false, Image.Format.Rgba8);
		ImageTexture Tex = ImageTexture.CreateFromImage(Img);
		Texture = Tex;

		// create image and texture to be used by that shader and visual updates
		ChunkImage = Image.CreateEmpty(ChunkSize, ChunkSize, false, Image.Format.Rgba8);
		ChunkTexture = ImageTexture.CreateFromImage(ChunkImage);
		TextureFilter = TextureFilterEnum.Nearest;


		Mat.Shader = RenderShader;
		Material = Mat;


		Scale = new Vector2(PixelScale, PixelScale);
		CustomMinimumSize = new Vector2(ChunkSize, ChunkSize);


		Mat.SetShaderParameter("cell_tex", ChunkTexture);
	}


}
