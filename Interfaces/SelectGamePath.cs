using Godot;
using System;

public partial class SelectGamePath : FileDialog
{
    public override void _Ready()
    {
        base._Ready();
        CloseRequested += QueueFree;
    }
}
