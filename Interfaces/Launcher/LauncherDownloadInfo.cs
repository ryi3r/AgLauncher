using Godot;
using System;

public partial class LauncherDownloadInfo : Panel
{
    public Button? Close;
    public Label? TopText;
    public ProgressBar? ProgressBar;
    public Label? EstimatedTime;
    public Label? TotalSize;
    public Label? Speed;
    public Label? Progress;


    public Label? BaseEstimatedTime;
    public Label? BaseTotalSize;
    public Label? BaseSpeed;
    public Label? BaseProgress;

    public override void _Ready()
    {
        base._Ready();

        Close = GetNode<Button>("Close");
        TopText = GetNode<Label>("VBox1/Text");
        ProgressBar = GetNode<ProgressBar>("VBox1/ProgressBar");
        EstimatedTime = GetNode<Label>("VBox2/EstimatedTime/Text");
        TotalSize = GetNode<Label>("VBox2/TotalSize/Text");
        Speed = GetNode<Label>("VBox3/Speed/Text");
        Progress = GetNode<Label>("VBox3/Progress/Text");

        BaseEstimatedTime = GetNode<Label>("VBox2/EstimatedTime");
        BaseTotalSize = GetNode<Label>("VBox2/TotalSize");
        BaseSpeed = GetNode<Label>("VBox3/Speed");
        BaseProgress = GetNode<Label>("VBox3/Progress");
    }
}
