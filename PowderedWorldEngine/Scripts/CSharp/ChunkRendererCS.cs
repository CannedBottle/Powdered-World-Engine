using Godot;
using System;

[GlobalClass]
public partial class ChunkRendererCS : TextureRect
{
	
	[Signal] public delegate void CellsUpdatedEventHandler();

	[Export] public int ChunkSize = 30;
	[Export] public int PixelScale = 10;
	[Export] public bool ShowDebugInfo = false;


	private static readonly Shader RenderShader = GD.Load<Shader>("uid://dtywnulygwh48");

	ShaderMaterial Mat = new ShaderMaterial();

	public Vector4[] CellValues;
	
	

	public override void _Ready()
	{
		CellsUpdated += WhenCellsUpdated;


		// create an Image to assign to this textureRect with the dimensions needed for the shader
		Image Img = Image.CreateEmpty(ChunkSize, ChunkSize, false, Image.Format.Rgba8);
		ImageTexture Tex = ImageTexture.CreateFromImage(Img);
		Texture = Tex;


		Mat.Shader = RenderShader;
		Material = Mat;

		Scale = new Vector2(PixelScale, PixelScale);
		CustomMinimumSize = new Vector2(ChunkSize, ChunkSize);
		CellValues = new Vector4[ChunkSize * ChunkSize];
		EmitSignal(SignalName.CellsUpdated);

	}

	
	private void WhenCellsUpdated()
	{
		Mat.SetShaderParameter("cells", CellValues);
	}

}
