using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Genshin.Sophon;
using Godot;

public partial class LauncherStartup : Control
{
    public readonly static System.Threading.Mutex Mutex = new();
    static bool InternalAreAllTasksOk = true;
    public static bool AreAllTasksOk
    {
        set
        {
            Mutex.WaitOne();
            try
            {
                InternalAreAllTasksOk = value;
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
                return InternalAreAllTasksOk;
            }
            finally
            {
                Mutex.ReleaseMutex();
            }
        }
    }

    public override async void _Ready()
    {
        base._Ready();

        Global.GameImages.Clear();
        LauncherData.Games.Clear();

        SettingsData.Load();

        var scroll = GetNode<VBoxContainer>("VBox/Scroll/VBox");
        var tasks = new List<Task>();
        {
            var eProg = GD.Load<PackedScene>("uid://87uaksqb1rd5").Instantiate<EntryProgress>();
            eProg.Text = "Check Wine version";
            eProg.CurrentStatus = EntryProgress.EntryStatus.Loading;
            scroll.AddChild(eProg);

            tasks.Add(Task.Run(async () =>
            {
                var isOk = true;
                try
                {
                    var wine = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = "wine",
                            Arguments = "--version",
                            RedirectStandardError = true,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true,
                        },
                    };
                    var wineInstalled = true;
                    try
                    {
                        wine.Start();
                        await wine.WaitForExitAsync();
                    }
                    catch (Exception)
                    {
                        wineInstalled = false;
                    }
                    if (wineInstalled)
                        Global.SystemWineVersion = await wine.StandardOutput.ReadLineAsync();
                }
                catch (Exception ex)
                {
                    GD.Print(ex);
                    isOk = false;
                    AreAllTasksOk = false;
                }
                eProg.CurrentStatus = isOk ? EntryProgress.EntryStatus.Ok : EntryProgress.EntryStatus.Error;
            }));
        }
        {
            var eProg1 = GD.Load<PackedScene>("uid://87uaksqb1rd5").Instantiate<EntryProgress>();
            eProg1.Text = "Obtain m!i!H!o!y!o! game entries".Replace("!", "");
            eProg1.CurrentStatus = EntryProgress.EntryStatus.Loading;
            scroll.AddChild(eProg1);

            var task = Task.Run(async () =>
            {
                var isOk = true;
                try
                {
                    await Sophon.FetchGameInfo();
                }
                catch (Exception ex)
                {
                    GD.Print(ex);
                    isOk = false;
                    AreAllTasksOk = false;
                }
                eProg1.CurrentStatus = isOk ? EntryProgress.EntryStatus.Ok : EntryProgress.EntryStatus.Error;
            });
            tasks.Add(task);
            var eProg2 = GD.Load<PackedScene>("uid://87uaksqb1rd5").Instantiate<EntryProgress>();
            eProg2.Text = "Obtain m!i!H!o!y!o! game images".Replace("!", "");
            scroll.AddChild(eProg2);
            tasks.Add(Task.Run(async () =>
            {
                await task;
                eProg2.CurrentStatus = EntryProgress.EntryStatus.Loading;
                var isOk = true;
                Image loadImageFromBuffer(byte[] data)
                {
                    var img = new Image();
                    if (data.AsSpan()[..8].SequenceEqual(new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                        img.LoadPngFromBuffer(data);
                    else if (data.AsSpan()[..3].SequenceEqual(new byte[3] { 255, 216, 255 }))
                        img.LoadJpgFromBuffer(data);
                    else if (data.AsSpan()[..4].SequenceEqual("RIFF"u8))
                        img.LoadWebpFromBuffer(data);
                    else if (data.AsSpan()[..2].SequenceEqual("BM"u8))
                        img.LoadBmpFromBuffer(data);
                    else
                        throw new NotSupportedException();
                    return img;
                }
                try
                {
                    var tasks = new List<Task>();
                    using var hc = new System.Net.Http.HttpClient();
                    foreach (var game in Sophon.GameInfo!.Games!)
                    {
                        List<Image?> gImg = [null, null, null, null];
                        tasks.Add(Task.Run(async () =>
                        {
                            if (game.Display!.Background != null)
                                gImg[Sophon.GameImagesBackground] = loadImageFromBuffer(await hc.GetByteArrayAsync(game.Display.Background!.Url!));
                            if (game.Display!.Logo != null)
                                gImg[Sophon.GameImagesLogo] = loadImageFromBuffer(await hc.GetByteArrayAsync(game.Display.Logo!.Url!));
                            if (game.Display!.Thumbnail != null)
                                gImg[Sophon.GameImagesThumbnail] = loadImageFromBuffer(await hc.GetByteArrayAsync(game.Display.Thumbnail!.Url!));
                            if (game.Display!.Icon != null)
                                gImg[Sophon.GameImagesIcon] = loadImageFromBuffer(await hc.GetByteArrayAsync(game.Display.Icon!.Url!));
                        }));
                        Global.GameImages.Add(game.Id!, gImg);
                    }
                    foreach (var task in tasks)
                        await task;
                }
                catch (Exception ex)
                {
                    GD.Print(ex);
                    isOk = false;
                    AreAllTasksOk = false;
                }
                eProg2.CurrentStatus = isOk ? EntryProgress.EntryStatus.Ok : EntryProgress.EntryStatus.Error;
            }));
            var eProg3 = GD.Load<PackedScene>("uid://87uaksqb1rd5").Instantiate<EntryProgress>();
            eProg3.Text = "Obtain m!i!H!o!y!o! game keys".Replace("!", "");
            scroll.AddChild(eProg3);
            tasks.Add(Task.Run(async () =>
            {
                await task;
                eProg3.CurrentStatus = EntryProgress.EntryStatus.Loading;
                var isOk = true;
                try
                {
                    var tasks = new List<Task>();
                    using var hc = new System.Net.Http.HttpClient();
                    foreach (var game in Sophon.GameInfo!.Games!)
                    {
                        var sophon = new Sophon(SettingsData.GameSettings[game.Id!].InstallationFolder ?? "")
                        {
                            GameId = game.Id!,
                            GameName = game.Display!.Name!,
                        };
                        LauncherData.Games.Add(sophon);
                        tasks.Add(Task.Run(async () =>
                        {
                            await sophon.RetrieveApiKeys();
                            if (sophon.BranchInfo!.Branches!.Count > 0)
                                sophon.TargetVersion = sophon.BranchInfo.Branches.First().Main!.Tag!;
                        }));
                    }
                    foreach (var task in tasks)
                        await task;
                }
                catch (Exception ex)
                {
                    GD.Print(ex);
                    isOk = false;
                    AreAllTasksOk = false;
                }
                eProg3.CurrentStatus = isOk ? EntryProgress.EntryStatus.Ok : EntryProgress.EntryStatus.Error;
            }));
        }
        {
            var eProg1 = GD.Load<PackedScene>("uid://87uaksqb1rd5").Instantiate<EntryProgress>();
            eProg1.Text = "Fetch Wine & Dxvk indexers";
            scroll.AddChild(eProg1);

            var task = Task.Run(async () =>
            {
                using var hc = new System.Net.Http.HttpClient();
                eProg1.CurrentStatus = EntryProgress.EntryStatus.Loading;
                var isOk = true;
                try
                {
                    Global.WineComponents = await hc.GetFromJsonAsync<WineComponents>(SettingsData.GetComponentsUrl("components.json"), new JsonSerializerOptions() { IncludeFields = true });
                }
                catch (Exception ex)
                {
                    GD.Print(ex);
                    isOk = false;
                    AreAllTasksOk = false;
                }
                eProg1.CurrentStatus = isOk ? EntryProgress.EntryStatus.Ok : EntryProgress.EntryStatus.Error;
            });
            tasks.Add(task);

            var eProg2 = GD.Load<PackedScene>("uid://87uaksqb1rd5").Instantiate<EntryProgress>();
            eProg2.Text = "Fetch Wine versions";
            scroll.AddChild(eProg2);
            tasks.Add(Task.Run(async () =>
            {
                await task;
                using var hc = new System.Net.Http.HttpClient();
                eProg2.CurrentStatus = EntryProgress.EntryStatus.Loading;
                var isOk = true;
                try
                {
                    foreach (var item in Global.WineComponents!.Wine!)
                    {
                        var tries = 0;
                        List<WineData>? req = null;
                        while (req == null)
                        {
                            try
                            {
                                req = await hc.GetFromJsonAsync<List<WineData>>(
                                    SettingsData.GetComponentsUrl($"wine/{item.Name}.json"),
                                    new JsonSerializerOptions() { IncludeFields = true }
                                );
                            }
                            catch (HttpRequestException ex)
                            {
                                tries++;
                                if (tries >= 20)
                                {
                                    GD.Print(ex);
                                    throw;
                                }
                            }
                        }
                        Global.WineData.Add(item.Name!, req);
                    }
                }
                catch (Exception ex)
                {
                    GD.Print(ex);
                    isOk = false;
                    AreAllTasksOk = false;
                }
                eProg2.CurrentStatus = isOk ? EntryProgress.EntryStatus.Ok : EntryProgress.EntryStatus.Error;
            }));

            var eProg3 = GD.Load<PackedScene>("uid://87uaksqb1rd5").Instantiate<EntryProgress>();
            eProg3.Text = "Fetch Dxvk versions";
            scroll.AddChild(eProg3);
            tasks.Add(Task.Run(async () =>
            {
                await task;
                using var hc = new System.Net.Http.HttpClient();
                eProg3.CurrentStatus = EntryProgress.EntryStatus.Loading;
                var isOk = true;
                try
                {
                    foreach (var item in Global.WineComponents!.Dxvk!)
                    {
                        var tries = 0;
                        List<DxvkData>? req = null;
                        while (req == null)
                        {
                            try
                            {
                                req = await hc.GetFromJsonAsync<List<DxvkData>>(
                                    SettingsData.GetComponentsUrl($"dxvk/{item.Name}.json"),
                                    new JsonSerializerOptions() { IncludeFields = true }
                                );
                            }
                            catch (HttpRequestException ex)
                            {
                                tries++;
                                if (tries >= 20)
                                {
                                    GD.Print(ex);
                                    throw;
                                }
                            }
                        }
                        Global.DxvkData.Add(item.Name!, req);
                    }
                }
                catch (Exception ex)
                {
                    GD.Print(ex);
                    isOk = false;
                    AreAllTasksOk = false;
                }
                eProg3.CurrentStatus = isOk ? EntryProgress.EntryStatus.Ok : EntryProgress.EntryStatus.Error;
            }));
        }
        foreach (var task in tasks)
            await task;
        if (!AreAllTasksOk)
            return;
        if (!Directory.Exists(SettingsData.InstallationFolder))
            SettingsData.HasSetup = false;
        else if (!Directory.Exists($"{SettingsData.InstallationFolder}/wine") /*|| !Directory.Exists($"{SettingsData.InstallationFolder}/dxvk")*/)
            SettingsData.HasSetup = false;
        if (!SettingsData.HasSetup)
        {
            SettingsData.HasSetup = true;
            var settings = GD.Load<PackedScene>("uid://c4eidf7xkwd1p").Instantiate<LauncherSettings>();
            settings.InitialSetup = true;
            AddChild(settings);
            await ToSignal(settings, Node.SignalName.TreeExited);
        }
        GetTree().ChangeSceneToFile("uid://2d5t40emmx5w");
    }
}
