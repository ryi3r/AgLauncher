using Godot;
using System;

public partial class LauncherGameLogo : Control
{
    public Sprite2D? MaskBgnd;
    public Sprite2D? MaskFore;
    public Sprite2D? Logo;
    public Button? Button;

    public override void _Ready()
    {
        base._Ready();

        MaskBgnd = GetNode<Sprite2D>("MaskBgnd");
        MaskFore = GetNode<Sprite2D>("MaskFore");
        Logo = GetNode<Sprite2D>("MaskFore/Logo");
        Button = GetNode<Button>("Button");
    }
}
