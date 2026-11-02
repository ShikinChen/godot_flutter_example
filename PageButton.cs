using Godot;
using System;

public partial class PageButton : Button
{
	public override void _Ready()
	{
		AddThemeFontSizeOverride("font_size", 64);
		Pressed += OnButtonPressed;
	}

	private void OnButtonPressed()
	{
		GetTree().ChangeSceneToFile("res://page_a.tscn");
	}
}
