using Godot;
using System;

public partial class EntryProgress : Label
{
    public enum EntryStatus
    {
        None,
        Error,
        Loading,
        Ok,
    }

    public Label? Status;
    public double Timer;
    public EntryStatus CurrentStatus = EntryStatus.None;

    public override void _Ready()
    {
        base._Ready();
        Status = GetNode<Label>("Status");
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        Size = Vector2.Zero;
        ForceUpdateTransform();
        Status!.Position = new(Size.X + 12.0f, 0.0f);
        if (CurrentStatus != EntryStatus.Loading || Timer >= 1.0)
            Timer = 0.0;
        else
            Timer += delta;
        switch (CurrentStatus)
        {
            case EntryStatus.None:
                Status.Modulate = Colors.White;
                Status.Text = "";
                break;
            case EntryStatus.Error:
                Status.Modulate = Colors.Red;
                Status.Text = "ERR";
                break;
            case EntryStatus.Loading:
                Status.Modulate = Colors.White;
                Status.Text = new string('.', (int)Math.Ceiling(Timer * 3.0));
                break;
            case EntryStatus.Ok:
                Status.Modulate = Colors.Lime;
                Status.Text = "OK";
                break;
        }
    }
}
