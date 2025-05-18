using Bottles.LibraryWine;
using Genshin.Sophon;
using Godot;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public partial class LauncherSettings : Window
{
    public bool InitialSetup = false;

    public Control? MainParent;
    public TextureRect? Background;
    public Button? Apply;
    public Label? ApplyLabel;
    public LineEdit? RepoLink;
    public Button? ReloadRepo;
    public OptionButton? WineType;
    public OptionButton? WineVersion;
    public OptionButton? DxvkType;
    public OptionButton? DxvkVersion;
    public LineEdit? InstallationFolderPath;

    public Control? WineBox;
    public Control? GeneralBox;
    public Control? GameBox;

    public (string, string) SettingsWineVersion = SettingsData.WineVersion;
    public (string, string) SettingsDxvkVersion = SettingsData.DxvkVersion;
    public string? SettingsInstallationFolder = SettingsData.InstallationFolder;
    public string SettingsWineRepo = SettingsData.WineRepo;
    public GameSettings? GameSettings = LauncherData.SelectedGameId != null ? (SettingsData.GameSettings.TryGetValue(LauncherData.SelectedGameId, out GameSettings? value) ? (GameSettings)value!.Clone() : null) : null;

    public bool DisableClose = false;

    public override void _Ready()
    {
        base._Ready();

        MainParent = GetNode<Control>("Control");
        Background = GetNode<TextureRect>("Control/Background");

        Apply = GetNode<Button>("Control/Container/Button");
        ApplyLabel = GetNode<Label>("Control/Container/Status");

        RepoLink = GetNode<LineEdit>("Control/TabContainer/Wine/Wine/Repo/Link");
        ReloadRepo = GetNode<Button>("Control/TabContainer/Wine/Wine/Repo/Button");

        WineType = GetNode<OptionButton>("Control/TabContainer/Wine/Wine/Wine/Type");
        WineVersion = GetNode<OptionButton>("Control/TabContainer/Wine/Wine/Wine/Version");

        DxvkType = GetNode<OptionButton>("Control/TabContainer/Wine/Wine/Dxvk/Type");
        DxvkVersion = GetNode<OptionButton>("Control/TabContainer/Wine/Wine/Dxvk/Version");

        InstallationFolderPath = GetNode<LineEdit>("Control/TabContainer/General/General/LauncherFolder/Path");

        WineBox = GetNode<Control>("Control/TabContainer/Wine");
        GeneralBox = GetNode<Control>("Control/TabContainer/General");
        GameBox = GetNode<Control>("Control/TabContainer/Current Game");

        if (Global.GameImages.Count > 0)
        {
            var timg = Global.GameImages.First(x => LauncherData.SelectedGameId == null || x.Key == LauncherData.SelectedGameId).Value[Sophon.GameImagesBackground];
            if (timg != null)
            {
                var img = new Image();
                img.CopyFrom(timg);
                img.Resize(16, 9, Image.Interpolation.Lanczos);
                img.Resize(16 * 8, 9 * 8, Image.Interpolation.Lanczos);
                Background.Texture = ImageTexture.CreateFromImage(img);
            }
        }

        WineBox.Visible = true;
        GeneralBox.Visible = false;
        GameBox.Visible = false;

        if (InitialSetup)
        {
            Title = "Initial Setup";
            Apply.Text = "Done";

            GameBox.QueueFree();
        }
        else
        {
            GameBox.GetNode<LineEdit>("Current Game/GameFolder/Path").Text = GameSettings!.InstallationFolder;
            var sophonNode = GameBox.GetNode<VBoxContainer>("Current Game/Sophon");
            sophonNode.Visible = false;
            if (GameSettings is SophonGameSettings sophon)
            {
                sophonNode.Visible = true;
                if (sophon.AudioRegions.Contains("en-us"))
                    sophonNode.GetNode<CheckBox>("AudioRegions/English").ButtonPressed = true;
                if (sophon.AudioRegions.Contains("ja-jp"))
                    sophonNode.GetNode<CheckBox>("AudioRegions/Japanese").ButtonPressed = true;
                if (sophon.AudioRegions.Contains("ko-kr"))
                    sophonNode.GetNode<CheckBox>("AudioRegions/Korean").ButtonPressed = true;
                if (sophon.AudioRegions.Contains("zh-cn"))
                    sophonNode.GetNode<CheckBox>("AudioRegions/Chinese").ButtonPressed = true;

                sophonNode.GetNode<SpinBox>("DownloadThreads/Value").Value = sophon.DownloadThreads;
                sophonNode.GetNode<SpinBox>("CheckThreads/Value").Value = sophon.CheckThreads;
                sophonNode.GetNode<SpinBox>("DeleteThreads/Value").Value = sophon.DeleteThreads;
                sophonNode.GetNode<SpinBox>("UpdateThreads/Value").Value = sophon.UpdateThreads;
                sophonNode.GetNode<SpinBox>("UpdateDecodeThreads/Value").Value = sophon.UpdateDecodeThreads;
            }
            {
                var game = LauncherData.Games[LauncherData.SelectedGame];
                if (game is not Sophon)
                    sophonNode.QueueFree();
            }
            CloseRequested += () =>
            {
                if (!DisableClose)
                    QueueFree();
            };
        }

        InstallationFolderPath.Text = SettingsInstallationFolder;
        RepoLink.Text = SettingsWineRepo;

        ReloadWineComponents();
        ReloadWineVersions();

        // todo: make reload repo button work
    }

    public List<string> WineTypes = [];
    public List<string> DxvkTypes = [];

    public void ReloadWineComponents()
    {
        WineType!.Clear();
        DxvkType!.Clear();
        WineTypes.Clear();
        DxvkTypes.Clear();

        foreach (var comp in Global.WineComponents!.Wine!)
        {
            WineType.AddItem(comp.Title!, WineTypes.Count);
            WineTypes.Add(comp.Name!);
        }
        foreach (var comp in Global.WineComponents!.Dxvk!)
        {
            DxvkType.AddItem(comp.Title!, DxvkTypes.Count);
            DxvkTypes.Add(comp.Name!);
        }

        WineType.AddItem("Custom", WineTypes.Count);
        WineTypes.Add("custom");
        if (Global.SystemWineVersion != null)
        {
            WineType.AddItem("System", WineTypes.Count);
            WineTypes.Add("system");
        }

        DxvkType.AddItem("Custom", DxvkTypes.Count);
        DxvkTypes.Add("custom");

        if (SettingsWineVersion.Item1 == "system")
            WineType.Select(WineTypes.FindIndex(x => x == "system"));
        else if (SettingsWineVersion.Item1 == "custom")
            WineType.Select(WineTypes.FindIndex(x => x == "custom"));
        else if (Global.WineData.ContainsKey(SettingsWineVersion.Item1))
            WineType.Select(WineTypes.FindIndex(x => x == SettingsWineVersion.Item1));

        if (SettingsDxvkVersion.Item1 == "custom")
            DxvkType.Select(DxvkTypes.FindIndex(x => x == "custom"));
        else if (Global.DxvkData.ContainsKey(SettingsDxvkVersion.Item1))
            DxvkType.Select(DxvkTypes.FindIndex(x => x == SettingsDxvkVersion.Item1));
    }

    public List<string> WineVersions = [];
    public List<string> DxvkVersions = [];

    public void ReloadWineVersions()
    {
        WineVersion!.Clear();
        DxvkVersion!.Clear();
        WineVersions.Clear();
        DxvkVersions.Clear();

        WineVersion.Visible = true;
        DxvkVersion.Visible = true;
        var winePath = GetNode<LineEdit>("Control/TabContainer/Wine/Wine/Wine/Path");
        var wineButton = GetNode<Button>("Control/TabContainer/Wine/Wine/Wine/Button");
        var dxvkPath = GetNode<LineEdit>("Control/TabContainer/Wine/Wine/Dxvk/Path");
        var dxvkButton = GetNode<Button>("Control/TabContainer/Wine/Wine/Dxvk/Button");
        winePath.Visible = false;
        wineButton.Visible = false;
        dxvkPath.Visible = false;
        dxvkButton.Visible = false;

        switch (SettingsWineVersion.Item1)
        {
            case "system":
                WineVersion.AddItem(Global.SystemWineVersion ?? "Unknown", WineVersions.Count);
                WineVersions.Add("system");
                break;
            case "custom":
                WineVersion.Visible = false;
                winePath.Visible = true;
                wineButton.Visible = true;
                break;
            default:
                foreach (var comp in Global.WineData![SettingsWineVersion.Item1])
                {
                    WineVersion.AddItem(comp.Title!, WineVersions.Count);
                    WineVersions.Add(comp.Name!);
                }

                if (WineVersions.Contains(SettingsWineVersion.Item2))
                    WineVersion.Select(WineVersions.FindIndex(x => x == SettingsWineVersion.Item2));
                break;
        }
        switch (SettingsDxvkVersion.Item1)
        {
            case "custom":
                DxvkVersion.Visible = false;
                dxvkPath.Visible = true;
                dxvkButton.Visible = true;
                break;
            default:
                foreach (var comp in Global.DxvkData![SettingsDxvkVersion.Item1])
                {
                    DxvkVersion.AddItem(comp.Title!, DxvkVersions.Count);
                    DxvkVersions.Add(comp.Name!);
                }

                if (DxvkVersions.Contains(SettingsDxvkVersion.Item2))
                    DxvkVersion.Select(DxvkVersions.FindIndex(x => x == SettingsDxvkVersion.Item2));
                break;
        }
    }

    public async void OnSelectInstallationFolder()
    {
        var fileDiag = GD.Load<PackedScene>("uid://cx8fl81ivbjoj").Instantiate<SelectGamePath>();
        AddChild(fileDiag);
        fileDiag.DirSelected += dir =>
        {
            SettingsInstallationFolder = dir;
            InstallationFolderPath!.Text = dir;
        };
        fileDiag.Show();
        await ToSignal(fileDiag, Node.SignalName.TreeExited);
    }

    public async void OnSelectGameFolder()
    {
        var fileDiag = GD.Load<PackedScene>("uid://cx8fl81ivbjoj").Instantiate<SelectGamePath>();
        AddChild(fileDiag);
        fileDiag.DirSelected += dir =>
        {
            if (GameSettings != null)
                GameSettings.InstallationFolder = dir;
            GameBox!.GetNode<LineEdit>("Current Game/GameFolder/Path").Text = dir;
        };
        fileDiag.Show();
        await ToSignal(fileDiag, Node.SignalName.TreeExited);
    }

    public async void OnSelectWineFolder()
    {
        var fileDiag = GD.Load<PackedScene>("uid://cx8fl81ivbjoj").Instantiate<SelectGamePath>();
        AddChild(fileDiag);
        fileDiag.DirSelected += dir =>
        {
            SettingsWineVersion.Item2 = dir;
            GetNode<LineEdit>("Control/TabContainer/Wine/Wine/Wine/Path").Text = dir;
        };
        fileDiag.Show();
        await ToSignal(fileDiag, Node.SignalName.TreeExited);
    }

    public async void OnSelectDxvkFolder()
    {
        var fileDiag = GD.Load<PackedScene>("uid://cx8fl81ivbjoj").Instantiate<SelectGamePath>();
        AddChild(fileDiag);
        fileDiag.DirSelected += dir =>
        {
            SettingsDxvkVersion.Item2 = dir;
            GetNode<LineEdit>("Control/TabContainer/Wine/Wine/Dxvk/Path").Text = dir;
        };
        fileDiag.Show();
        await ToSignal(fileDiag, Node.SignalName.TreeExited);
    }

    public void OnWineTypeSelected(int index)
    {
        if (index != -1)
        {
            SettingsWineVersion.Item1 = WineTypes[index];
            switch (SettingsWineVersion.Item1)
            {
                case "system":
                    SettingsWineVersion.Item2 = "";
                    break;
                case "custom":
                    SettingsWineVersion.Item2 = GetNode<LineEdit>("Control/TabContainer/Wine/Wine/Wine/Path").Text;
                    break;
                default:
                    SettingsWineVersion.Item2 = Global.WineData![SettingsWineVersion.Item1].First().Name!;
                    break;
            }

            ReloadWineComponents();
            ReloadWineVersions();
        }
    }

    public void OnDxvkTypeSelected(int index)
    {
        if (index != -1)
        {
            SettingsDxvkVersion.Item1 = DxvkTypes[index];
            switch (SettingsDxvkVersion.Item1)
            {
                case "custom":
                    SettingsDxvkVersion.Item2 = GetNode<LineEdit>("Control/TabContainer/Wine/Wine/Dxvk/Path").Text;
                    break;
                default:
                    SettingsDxvkVersion.Item2 = Global.DxvkData![SettingsDxvkVersion.Item1].First().Name!;
                    break;
            }

            ReloadWineComponents();
            ReloadWineVersions();
        }
    }

    public void OnWineVersionSelected(int index)
    {
        if (index != -1)
        {
            SettingsWineVersion.Item2 = Global.WineData![SettingsWineVersion.Item1][index].Name!;
            ReloadWineVersions();
        }
    }

    public void OnDxvkVersionSelected(int index)
    {
        if (index != -1)
        {
            SettingsDxvkVersion.Item2 = Global.DxvkData![SettingsDxvkVersion.Item1][index].Name!;
            ReloadWineVersions();
        }
    }

    public void OnSelectRepairGame()
    {
        if (GameSettings != null)
        {
            var game = LauncherData.Games[LauncherData.SelectedGame];
            if (game.State == Base.GameState.None)
                game.Repair(LauncherData.GameCancellationTokens[LauncherData.SelectedGameId!]);
        }
    }

    public async void OnSelectUninstallGame()
    {
        if (GameSettings != null)
        {
            var game = LauncherData.Games[LauncherData.SelectedGame];
            if (game.State == Base.GameState.None)
            {
                await game.Delete(LauncherData.GameCancellationTokens[LauncherData.SelectedGameId!]);
                var selectedGame = LauncherData.Games[LauncherData.SelectedGame];
                var gameSettings = SettingsData.GameSettings[LauncherData.SelectedGameId!];
                selectedGame.GameDirectory = gameSettings.InstallationFolder!;
                LauncherData.InstalledGameVersion = selectedGame.GetInstalledGameVersion();
                LauncherData.GameVersion!.Text = $"{selectedGame.GameName} {selectedGame.TargetVersion}\nInstalled Ver. {LauncherData.InstalledGameVersion ?? "?.?.?"}";
            }
        }
    }

    public void OnApplyPressed()
    {
        DisableClose = true;
        ApplyLabel!.Modulate = Colors.White;
        if (GameSettings != null)
        {
            GameSettings.InstallationFolder = GameBox!.GetNode<LineEdit>("Current Game/GameFolder/Path").Text;
            GameSettings.LaunchScript = GameBox!.GetNode<TextEdit>("Current Game/LaunchScript/Text").Text;
            if (GameSettings is SophonGameSettings sophon)
            {
                sophon.AudioRegions.Clear();
                if (GameBox.GetNode<CheckBox>("Current Game/Sophon/AudioRegions/English").ButtonPressed)
                    sophon.AudioRegions.Add("en-us");
                if (GameBox.GetNode<CheckBox>("Current Game/Sophon/AudioRegions/Japanese").ButtonPressed)
                    sophon.AudioRegions.Add("ja-jp");
                if (GameBox.GetNode<CheckBox>("Current Game/Sophon/AudioRegions/Korean").ButtonPressed)
                    sophon.AudioRegions.Add("ko-kr");
                if (GameBox.GetNode<CheckBox>("Current Game/Sophon/AudioRegions/Chinese").ButtonPressed)
                    sophon.AudioRegions.Add("zh-cn");

                sophon.DownloadThreads = (int)GameBox.GetNode<SpinBox>("Current Game/Sophon/DownloadThreads/Value").Value;
                sophon.CheckThreads = (int)GameBox.GetNode<SpinBox>("Current Game/Sophon/CheckThreads/Value").Value;
                sophon.DeleteThreads = (int)GameBox.GetNode<SpinBox>("Current Game/Sophon/DeleteThreads/Value").Value;
                sophon.UpdateThreads = (int)GameBox.GetNode<SpinBox>("Current Game/Sophon/UpdateThreads/Value").Value;
                sophon.UpdateDecodeThreads = (int)GameBox.GetNode<SpinBox>("Current Game/Sophon/UpdateDecodeThreads/Value").Value;
            }

            SettingsData.GameSettings[LauncherData.SelectedGameId!] = GameSettings;
            LauncherData.Games[LauncherData.SelectedGame].GameSettings = GameSettings;
        }
        Task.Run(async () =>
        {
            var ok = true;
            try
            {
                // todo: make it report download rate or something
                using var hc = new System.Net.Http.HttpClient();
                if (SettingsWineRepo != SettingsData.WineRepo)
                    SettingsData.WineRepo = SettingsWineRepo;
                SettingsData.InstallationFolder = SettingsInstallationFolder;
                var wineTrigger = false;
                if (SettingsWineVersion != SettingsData.WineVersion || !Directory.Exists(SettingsData.WineFolder) || !Directory.Exists($"{SettingsInstallationFolder}/wineprefix/"))
                {
                    wineTrigger = SettingsDxvkVersion == SettingsData.DxvkVersion;
                    SettingsData.WineVersion = SettingsWineVersion;
                    Callable.From(() => ApplyLabel!.Text = "Downloading Wine (0)...").CallDeferred();
                    {
                        var i = 0;
                        var list = Global.WineData![SettingsWineVersion.Item1];
                        var index = list.FindIndex(x => x.Name == SettingsWineVersion.Item2);
                        using var data = await hc.GetStreamAsync(list[index].Uri);
                        using var reader = ReaderFactory.Open(data);
                        var basePath = $"{SettingsInstallationFolder}/wine/";
                        Directory.CreateDirectory(basePath);
                        Directory.CreateDirectory($"{SettingsInstallationFolder}/wineprefix/");
                        SettingsData.WineFolder = null;
                        while (reader.MoveToNextEntry())
                        {
                            i++;
                            Callable.From(() => ApplyLabel!.Text = $"Downloading Wine ({i})...").CallDeferred();
                            //GD.Print(reader.Entry.Key);
                            var fpath = $"{basePath}/{reader.Entry.Key}";
                            if (reader.Entry.IsDirectory)
                            {
                                SettingsData.WineFolder ??= fpath;
                                Directory.CreateDirectory(fpath);
                            }
                            else
                            {
                                reader.WriteEntryToFile(fpath, new SharpCompress.Common.ExtractionOptions()
                                {
                                    Overwrite = true,
                                    WriteSymbolicLink = (source, target) =>
                                    {
                                        try
                                        {
                                            File.CreateSymbolicLink(source, target);
                                        }
                                        catch (IOException)
                                        {
                                            // todo: i'm not even sure what i'm doing wrong
                                            // but if the symbolic link already exists it explodes
                                            // i tried deleting the file but it didn't work...
                                            //File.Delete(source[..(source.LastIndexOf('/') + 1)] + target);
                                        }
                                    },
                                });
                                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && (new FileInfo(fpath).LinkTarget == null))
                                    File.SetUnixFileMode(fpath, File.GetUnixFileMode(fpath) | UnixFileMode.UserExecute | UnixFileMode.OtherExecute | UnixFileMode.GroupExecute);
                            }

                        }
                        Callable.From(() => ApplyLabel!.Text = "Setting up Wineprefix...").CallDeferred();
                        var wine = new Wine(
                            SettingsData.WineFolder,
                            $"{SettingsInstallationFolder}/wineprefix/",
                            Wine.VerboseLevels.FIXME_N_ALL,
                            Global.WineComponents!.Wine!.Find(x => x.Name == SettingsWineVersion.Item1)!.Features!.Bundle!.ToLowerInvariant() == "proton"
                        );
                        await File.WriteAllBytesAsync($"{wine.WinePath}/bin/winetricks", await hc.GetByteArrayAsync("https://raw.githubusercontent.com/Winetricks/winetricks/master/src/winetricks"));
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                            File.SetUnixFileMode($"{wine.WinePath}/bin/winetricks", File.GetUnixFileMode($"{wine.WinePath}/bin/winetricks") | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute | UnixFileMode.UserExecute);
                        //WineTools.WineBootInit(ref wine);
                        {
                            using var proc = new Process()
                            {
                                StartInfo = new()
                                {
                                    FileName = $"{wine.WinePath}/bin/wine64",
                                    Arguments = $"./bin/winetricks -q dxvk vkd3d vcrun2019",
                                    UseShellExecute = true,
                                    CreateNoWindow = true,
                                    WorkingDirectory = wine.WinePath,
                                },
                            };
                            proc.StartInfo.EnvironmentVariables["WINEPREFIX"] = wine.WinePrefixPath;
                            proc.StartInfo.EnvironmentVariables["WINEDEBUG"] = wine.VerboseLevel switch
                            {
                                Wine.VerboseLevels.N_ALL => "-all",
                                Wine.VerboseLevels.N_WARN_Y_ALL => "-warn+all",
                                Wine.VerboseLevels.FIXME_N_ALL => "fixme-all",
                                Wine.VerboseLevels.Y_ALL => "+all",
                                _ => throw new NotSupportedException(),
                            };
                            proc.Start();
                            await proc.WaitForExitAsync();
                        }
                    }
                }
                if (SettingsDxvkVersion != SettingsData.DxvkVersion || !Directory.Exists(SettingsData.DxvkFolder) || wineTrigger)
                {
                    SettingsData.DxvkVersion = SettingsDxvkVersion;
                    Callable.From(() => ApplyLabel!.Text = $"Downloading Dxvk (0)...").CallDeferred();
                    {
                        if (wineTrigger)
                        {
                            var i = 0;
                            var list = Global.DxvkData![SettingsDxvkVersion.Item1];
                            var index = list.FindIndex(x => x.Name == SettingsDxvkVersion.Item2);
                            using var data = await hc.GetStreamAsync(list[index].Uri);
                            using var reader = ReaderFactory.Open(data);
                            var basePath = $"{SettingsInstallationFolder}/dxvk/";
                            Directory.CreateDirectory(basePath);
                            SettingsData.DxvkFolder = null;
                            while (reader.MoveToNextEntry())
                            {
                                i++;
                                Callable.From(() => ApplyLabel!.Text = $"Downloading Dxvk ({i})...").CallDeferred();
                                //GD.Print(reader.Entry.Key);
                                var fpath = $"{basePath}/{reader.Entry.Key}";
                                if (reader.Entry.IsDirectory)
                                {
                                    SettingsData.DxvkFolder ??= fpath;
                                    Directory.CreateDirectory(fpath);
                                }
                                else
                                {
                                    reader.WriteEntryToFile(fpath, new SharpCompress.Common.ExtractionOptions()
                                    {
                                        Overwrite = true,
                                        WriteSymbolicLink = (source, target) =>
                                        {
                                            try
                                            {
                                                File.CreateSymbolicLink(source, target);
                                            }
                                            catch (IOException)
                                            {
                                                // todo: i'm not even sure what i'm doing wrong
                                                // but if the symbolic link already exists it explodes
                                                // i tried deleting the file but it didn't work...
                                                //File.Delete(source[..(source.LastIndexOf('/') + 1)] + target);
                                            }
                                        },
                                    });
                                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && (new FileInfo(fpath).LinkTarget == null))
                                        File.SetUnixFileMode(fpath, File.GetUnixFileMode(fpath) | UnixFileMode.UserExecute | UnixFileMode.OtherExecute | UnixFileMode.GroupExecute);
                                }
                            }
                        }
                        Callable.From(() => ApplyLabel!.Text = "Setting up Wineprefix...").CallDeferred();
                        var wine = new Wine(
                            SettingsData.WineFolder,
                            $"{SettingsInstallationFolder}/wineprefix/",
                            Wine.VerboseLevels.FIXME_N_ALL,
                            Global.WineComponents!.Wine!.Find(x => x.Name == SettingsWineVersion.Item1)!.Features!.Bundle!.ToLowerInvariant() == "proton"
                        );
                        {
                            using var f = File.Create($"{wine.WinePrefixPath}/setup.sh");
                            await f.WriteAsync($"#!/bin/bash\ncp x64/*.dll $WINEPREFIX/drive_c/windows/system32\ncp x32/*.dll $WINEPREFIX/drive_c/windows/syswow64".ToUtf8Buffer());
                            f.Close();
                        }
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                            File.SetUnixFileMode($"{wine.WinePrefixPath}/setup.sh", File.GetUnixFileMode($"{wine.WinePrefixPath}/setup.sh") | UnixFileMode.UserExecute | UnixFileMode.OtherExecute | UnixFileMode.GroupExecute);
                        {
                            using var proc = new Process()
                            {
                                StartInfo = new()
                                {
                                    FileName = $"{wine.WinePrefixPath}/setup.sh",
                                    UseShellExecute = true,
                                    CreateNoWindow = true,
                                    WorkingDirectory = SettingsData.DxvkFolder,
                                },
                            };
                            proc.StartInfo.EnvironmentVariables["WINEPREFIX"] = wine.WinePrefixPath;
                            proc.StartInfo.EnvironmentVariables["WINEDEBUG"] = wine.VerboseLevel switch
                            {
                                Wine.VerboseLevels.N_ALL => "-all",
                                Wine.VerboseLevels.N_WARN_Y_ALL => "-warn+all",
                                Wine.VerboseLevels.FIXME_N_ALL => "fixme-all",
                                Wine.VerboseLevels.Y_ALL => "+all",
                                _ => throw new NotSupportedException(),
                            };
                            proc.Start();
                            await proc.WaitForExitAsync();
                        }
                        foreach (var dll in new string[] { "d3d8", "d3d9", "d3d10core", "d3d11", "dxgi" })
                            WineRegister.AddDllOverride(ref wine, dll, WineRegister.DllOverrideTypes.NATIVE);
                    }
                }

                SettingsData.Save();
            }
            catch (Exception ex)
            {
                GD.Print(ex);
                ok = false;
            }
            DisableClose = false;
            if (ok)
                QueueFree();
            else
            {
                Callable.From(() =>
                {
                    ApplyLabel.Modulate = Colors.Red;
                    ApplyLabel.Text = ApplyLabel.Text.Length > 0 ? $"Errored on: {ApplyLabel.Text}" : "An error occurred";
                }).CallDeferred();
            }
        });
    }
}
