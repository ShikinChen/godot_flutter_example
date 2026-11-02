using Godot;
using System;

public partial class BackButton : Button
{
	public override void _Ready()
	{
		AddThemeFontSizeOverride("font_size", 64);

		Pressed += OnBackPressed;
	}

	private void OnBackPressed()
	{
		GetTree().ChangeSceneToFile("res://main.tscn");
	}
}
