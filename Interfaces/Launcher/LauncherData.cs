using Base;
using Bottles.LibraryWine;
using Genshin.Sophon;
using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

public partial class LauncherData : HBoxContainer
{
    public VBoxContainer? GameLogoContainer;
    public LauncherDownloadInfo? DownloadInfo;
    public Control? DataNode;
    public static Label? GameVersion;
    public TextureRect? GameBackground;
    public Button? DownloadButton;

    public static int SelectedGame = 0;
    public readonly static System.Threading.Mutex Mutex = new();
    static List<BaseGame> InnerGames = [];
    public static List<BaseGame> Games
    {
        set
        {
            Mutex.WaitOne();
            try
            {
                InnerGames = value;
            }
            finally
            {
                Mutex.ReleaseMutex();
            }
        }

        get
        {
            Mutex.WaitOne();
            try
            {
                return InnerGames;
            }
            finally
            {
                Mutex.ReleaseMutex();
            }
        }
    }
    public List<LauncherGameLogo> GameLogo = [];
    public static string? SelectedGameId;
    public static Dictionary<string, CancellationTokenSource> GameCancellationTokens = [];
    public static string? InstalledGameVersion;

    public override void _Ready()
    {
        base._Ready();

        GameLogoContainer = GetNode<VBoxContainer>("Games/Scroll/VBox");
        DataNode = GetNode<Control>("Data");
        GameVersion = GetNode<Label>("Data/Info/Version");
        GameBackground = GetNode<TextureRect>("Data/Background");
        DownloadButton = GetNode<Button>("Data/Button");

        foreach (var game in Games)
        {
            GameCancellationTokens.Add(game.GameId, new());
            if (game is Sophon sophon)
            {
                var gLogo = GD.Load<PackedScene>("uid://cflbrfbppny53").Instantiate<LauncherGameLogo>();
                GameLogoContainer.AddChild(gLogo);
                gLogo.Logo!.Texture = ImageTexture.CreateFromImage(Global.GameImages[sophon.GameId][Sophon.GameImagesIcon]);
                gLogo.Logo.Scale = new(512.0f / gLogo.Logo.Texture.GetWidth(), 512.0f / gLogo.Logo.Texture.GetHeight());
                gLogo.Button!.Pressed += () => UpdateSelectedGame(Games.FindIndex(x => x == sophon));
                GameLogo.Add(gLogo);
            }
        }

        UpdateSelectedGame(0);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        var selectedGame = Games[SelectedGame];

        if (selectedGame.State == GameState.None)
            DownloadButton!.Text = InstalledGameVersion != null ? (InstalledGameVersion != selectedGame.TargetVersion ? "Update Game" : "Run Game") : "Download Game";
        else
            DownloadButton!.Text = "Cancel Task";

        if (selectedGame.Status != null)
        {
            if (!IsInstanceValid(DownloadInfo))
            {
                DownloadInfo = GD.Load<PackedScene>("uid://4f6sctywiu8m").Instantiate<LauncherDownloadInfo>();
                DataNode!.AddChild(DownloadInfo);
                DataNode.MoveChild(DownloadInfo, 1);
            }
            DownloadInfo.BaseEstimatedTime!.Visible = true;
            DownloadInfo.BaseTotalSize!.Visible = true;
            DownloadInfo.BaseSpeed!.Visible = true;
            DownloadInfo.BaseProgress!.Visible = true;
            DownloadInfo.ProgressBar!.Visible = true;

            if (selectedGame.Status is GameDownload gDown)
            {
                gDown.DownloadRate.Update();
                {
                    var value = (double)gDown.DownloadRate.GetValue();
                    var sizeType = "B";
                    while (value >= 1024.0)
                    {
                        value /= 1024.0;
                        sizeType = sizeType switch
                        {
                            "B" => "kB",
                            "kB" => "MiB",
                            "MiB" => "GiB",
                            "GiB" => "TiB",
                            _ => ">TiB",
                        };
                    }
                    DownloadInfo.Speed!.Text = $"{Mathf.Snapped(value, 0.01):0.00} {sizeType}/s";
                }
                {
                    var value = (double)gDown.DownloadedData.GetValue();
                    var sizeType = "B";
                    while (value >= 1024.0)
                    {
                        value /= 1024.0;
                        sizeType = sizeType switch
                        {
                            "B" => "kB",
                            "kB" => "MiB",
                            "MiB" => "GiB",
                            "GiB" => "TiB",
                            _ => ">TiB",
                        };
                    }
                    DownloadInfo.Progress!.Text = $"{Mathf.Snapped((double)gDown.DownloadedData.GetValue() / Math.Max(gDown.TargetDownload.GetValue(), 1) * 100.0, 0.01):0.00}% ({Mathf.Snapped(value, 0.01):0.00} {sizeType})";
                }
                {
                    var value = (double)gDown.TargetDownload.GetValue();
                    var sizeType = "B";
                    while (value >= 1024.0)
                    {
                        value /= 1024.0;
                        sizeType = sizeType switch
                        {
                            "B" => "kB",
                            "kB" => "MiB",
                            "MiB" => "GiB",
                            "GiB" => "TiB",
                            _ => ">TiB",
                        };
                    }
                    DownloadInfo.TotalSize!.Text = $"{Mathf.Snapped(value, 0.01):0.00} {sizeType}";
                }
                if (gDown.IndeterminateProgressBar)
                    DownloadInfo.ProgressBar!.Indeterminate = true;
                else
                {
                    DownloadInfo.ProgressBar!.Indeterminate = false;
                    DownloadInfo.ProgressBar!.Value = (double)gDown.DownloadedData.GetValue() / Math.Max(gDown.TargetDownload.GetValue(), 1) * 100.0;
                }
                DownloadInfo.TopText!.Text = gDown.Message;
                if (gDown.DownloadedData.GetValue() >= gDown.TargetDownload.GetValue())
                    DownloadInfo.EstimatedTime!.Text = "--:--:--";
                else
                {
                    var validTime = gDown.DownloadedData.GetValue() != 0;
                    try
                    {
                        if (validTime)
                        {
                            var finalTime = gDown.DownloadRate.Timer.Elapsed * (((double)(gDown.TargetDownload.GetValue() - gDown.IgnoreProgress.GetValue()) - (gDown.DownloadedData.GetValue() - gDown.IgnoreProgress.GetValue())) / (1024.0f * 1024.0f)) / ((gDown.DownloadedData.GetValue() - gDown.IgnoreProgress.GetValue()) / (1024.0f * 1024.0f));
                            DownloadInfo.EstimatedTime!.Text = $"{finalTime.TotalHours:00}:{finalTime.Minutes:00}:{finalTime.Seconds:00}";
                        }
                    }
                    catch (OverflowException)
                    {
                        validTime = false;
                    }
                    if (!validTime)
                        DownloadInfo.EstimatedTime!.Text = "--:--:--";
                }
            }
            else if (selectedGame.Status is GameProgress gProg)
            {
                gProg.Rate.Update();
                {
                    var value = (double)gProg.Rate.GetValue();
                    var sizeType = "B";
                    while (value >= 1024.0)
                    {
                        value /= 1024.0;
                        sizeType = sizeType switch
                        {
                            "B" => "kB",
                            "kB" => "MiB",
                            "MiB" => "GiB",
                            "GiB" => "TiB",
                            _ => ">TiB",
                        };
                    }
                    DownloadInfo.Speed!.Text = $"{Mathf.Snapped(value, 0.01):0.00} {sizeType}/s";
                }
                DownloadInfo.BaseTotalSize!.Visible = false;
                DownloadInfo.Progress!.Text = $"{Mathf.Snapped((double)gProg.CurrentProgress.GetValue() / Math.Max(gProg.TargetProgress.GetValue(), 1) * 100.0, 0.01):0.00}%";
                if (gProg.IndeterminateProgressBar)
                    DownloadInfo.ProgressBar!.Indeterminate = true;
                else
                {
                    DownloadInfo.ProgressBar!.Indeterminate = false;
                    DownloadInfo.ProgressBar!.Value = (double)gProg.CurrentProgress.GetValue() / Math.Max(gProg.TargetProgress.GetValue(), 1) * 100.0;
                }
                DownloadInfo.TopText!.Text = gProg.Message;
                if (gProg.CurrentProgress.GetValue() >= gProg.TargetProgress.GetValue())
                    DownloadInfo.EstimatedTime!.Text = "--:--:--";
                else
                {
                    var validTime = gProg.CurrentProgress.GetValue() != 0;
                    try
                    {
                        if (validTime)
                        {
                            var finalTime = gProg.Rate.Timer.Elapsed * (((double)(gProg.TargetProgress.GetValue() - gProg.IgnoreProgress.GetValue()) - (gProg.CurrentProgress.GetValue() - gProg.IgnoreProgress.GetValue())) / (1024.0f * 1024.0f)) / ((gProg.CurrentProgress.GetValue() - gProg.IgnoreProgress.GetValue()) / (1024.0f * 1024.0f));
                            DownloadInfo.EstimatedTime!.Text = $"{finalTime.TotalHours:00}:{finalTime.Minutes:00}:{finalTime.Seconds:00}";
                        }
                    }
                    catch (OverflowException)
                    {
                        validTime = false;
                    }
                    if (!validTime)
                        DownloadInfo.EstimatedTime!.Text = "--:--:--";
                }
            }
            else
            {
                DownloadInfo!.BaseEstimatedTime!.Visible = false;
                DownloadInfo.BaseTotalSize!.Visible = false;
                DownloadInfo.BaseSpeed!.Visible = false;
                DownloadInfo.BaseProgress!.Visible = false;
                DownloadInfo.ProgressBar!.Visible = false;

                DownloadInfo.TopText!.Text = selectedGame.Status.Message;
            }
        }
        else if (IsInstanceValid(DownloadInfo))
            DownloadInfo.Free();
    }

    public void UpdateSelectedGame(int targetGame)
    {
        {
            var gLogo = GameLogo[SelectedGame]!;
            var tw = CreateTween();
            tw.TweenProperty(gLogo.MaskBgnd, "scale", new Vector2(46.0f / gLogo.MaskBgnd!.Texture.GetWidth(), 46.0f / gLogo.MaskBgnd!.Texture.GetHeight()), 1.0 / 4.0);
            tw.Play();
        }
        var lastSelectedGame = SelectedGame;
        SelectedGame = targetGame;
        {
            var gLogo = GameLogo[SelectedGame]!;
            var tw = CreateTween();
            tw.TweenProperty(gLogo.MaskBgnd, "scale", new Vector2(50.0f / gLogo.MaskBgnd!.Texture.GetWidth(), 50.0f / gLogo.MaskBgnd!.Texture.GetHeight()), 1.0 / 4.0);
            tw.Play();
        }
        var selectedGame = Games[SelectedGame]!;

        InstalledGameVersion = selectedGame.GetInstalledGameVersion();
        GameVersion!.Text = $"{selectedGame.GameName} {selectedGame.TargetVersion}\nInstalled Ver. {InstalledGameVersion ?? "?.?.?"}";

        if (selectedGame is Sophon sophon)
        {
            SelectedGameId = sophon.GameId;
            if (lastSelectedGame != SelectedGame)
            {
                var gBg = GameBackground!.Duplicate();
                GameBackground.AddChild(gBg);
                var tw = CreateTween();
                tw.TweenProperty(gBg, "modulate:a", 0.0f, 1.0 / 4.0);
                tw.TweenCallback(Callable.From(gBg.QueueFree));
                tw.Play();
            }
            GameBackground!.Texture = ImageTexture.CreateFromImage(Global.GameImages[sophon.GameId][Sophon.GameImagesBackground]);
            // todo: other shit
        }
        else
            throw new NotSupportedException();
    }

    public async void OnSettingsPressed()
    {
        {
            var selectedGame = Games[SelectedGame];
            var gameSettings = SettingsData.GameSettings[SelectedGameId!];
            selectedGame.GameDirectory = gameSettings.InstallationFolder!;
            selectedGame.GameSettings = gameSettings;
        }
        var settings = GD.Load<PackedScene>("uid://c4eidf7xkwd1p").Instantiate<LauncherSettings>();
        AddChild(settings);
        await ToSignal(settings, Node.SignalName.TreeExited);
        {
            var selectedGame = Games[SelectedGame];
            var gameSettings = SettingsData.GameSettings[SelectedGameId!];
            selectedGame.GameDirectory = gameSettings.InstallationFolder!;
            selectedGame.GameSettings = gameSettings;
            InstalledGameVersion = selectedGame.GetInstalledGameVersion();
            GameVersion!.Text = $"{selectedGame.GameName} {selectedGame.TargetVersion}\nInstalled Ver. {InstalledGameVersion ?? "?.?.?"}";
        }
    }

    public async void OnDownloadPressed()
    {
        var startSelected = SelectedGame;
        var selectedGame = Games[SelectedGame];
        var gameSettings = SettingsData.GameSettings[SelectedGameId!];
        selectedGame.GameDirectory = gameSettings.InstallationFolder!;
        selectedGame.GameSettings = gameSettings;
        if (selectedGame is Sophon sophon)
        {
            switch (selectedGame.State)
            {
                case GameState.None:
                    if (InstalledGameVersion != null)
                    {
                        var cToken = GameCancellationTokens[selectedGame.GameId];
                        var _ = Task.Run(async () =>
                        {
                            selectedGame.State = GameState.Running;
                            try
                            {
                                var wineData = Global.WineComponents!.Wine!.Find(x => x.Name == SettingsData.WineVersion.Item1)!;
                                var wine = new Wine(
                                    SettingsData.WineFolder,
                                    $"{SettingsData.InstallationFolder}/wineprefix/",
                                    Wine.VerboseLevels.N_ALL,
                                    wineData.Features!.Bundle!.ToLowerInvariant() == "proton"
                                );
                                //WineTools.WineBootInit(ref wine);
                                //WineTools.RunExe(ref wine, "winecfg");
                                var targetExec = "";
                                {
                                    var files = Directory.EnumerateFiles(selectedGame.GameDirectory);
                                    foreach (var file in files)
                                    {
                                        if (!file.Contains("UnityCrashHandler") && file.EndsWith(".exe"))
                                        {
                                            targetExec = file;
                                            break;
                                        }
                                    }
                                }
                                if (targetExec.Length > 0)
                                {
                                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                                    {
                                        var env = new Dictionary<string, string>();
                                        if (wineData.Features.Env != null)
                                        {
                                            foreach (var (key, value) in wineData.Features.Env)
                                                env.Add(key, value.ToString());
                                        }
                                        var dxvk = (Global.WineComponents!.Dxvk ?? []).Find(x => x.Name == SettingsData.DxvkVersion.Item1);
                                        if (dxvk != null && (dxvk.Features ?? new()).Env != null)
                                        {
                                            foreach (var (key, value) in dxvk.Features!.Env!)
                                                env.Add(key, value.ToString());
                                        }
                                        {
                                            using var f = File.Create($"{selectedGame.GameDirectory}/run.sh");
                                            f.Write(gameSettings.LaunchScript
                                                .Replace("%wine%", $"{wine.WinePath}/bin/wine64")
                                                .Replace("%executable%", targetExec)
                                                .ToUtf8Buffer()
                                            );
                                            f.Close();
                                        }
                                        File.SetUnixFileMode($"{selectedGame.GameDirectory}/run.sh", File.GetUnixFileMode($"{selectedGame.GameDirectory}/run.sh") | UnixFileMode.OtherExecute | UnixFileMode.GroupExecute | UnixFileMode.UserExecute);
                                        using var proc = new Process()
                                        {
                                            StartInfo = new()
                                            {
                                                FileName = "bash",
                                                Arguments = $"{selectedGame.GameDirectory}/run.sh",
                                                UseShellExecute = false,
                                                CreateNoWindow = true,
                                                WorkingDirectory = wine.WinePrefixPath,
                                                RedirectStandardOutput = true,
                                                RedirectStandardError = true,
                                            },
                                        };
                                        proc.Start();
                                        while (true)
                                        {
                                            if (proc.HasExited)
                                                break;
                                            else if (cToken.IsCancellationRequested)
                                            {
                                                proc.Kill(true);
                                                break;
                                            }
                                            await Task.Delay(100);
                                        }
                                    }
                                }

                            }
                            finally
                            {
                                selectedGame.State = GameState.None;
                            }
                        });
                    }
                    else
                    {
                        if (InstalledGameVersion != null && InstalledGameVersion != selectedGame.TargetVersion)
                            await sophon.Update(GameCancellationTokens[selectedGame.GameId]);
                        else
                            await sophon.Install(GameCancellationTokens[selectedGame.GameId]);
                        if (SelectedGame == startSelected)
                        {
                            InstalledGameVersion = selectedGame.GetInstalledGameVersion();
                            GameVersion!.Text = $"{selectedGame.GameName} {selectedGame.TargetVersion}\nInstalled Ver. {InstalledGameVersion ?? "?.?.?"}";
                        }
                    }
                    break;
                default:
                    GameCancellationTokens[selectedGame.GameId].Cancel();
                    GameCancellationTokens[selectedGame.GameId] = new();
                    break;
            }
        }
    }
}
