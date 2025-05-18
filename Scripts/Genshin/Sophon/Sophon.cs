namespace Genshin.Sophon;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Base;
using Genshin.Protobuf.Diff;
using Genshin.Protobuf.Manifest;
using global::Protobuf;
using Godot;
using SharpHDiffPatch.Core;
using ZstdSharp;

public class Sophon(string gameDirectory) : BaseGame(gameDirectory)
{
    public const string LauncherId = "VYTpXlbWo8";
    public GameInfoBranchData? BranchInfo;
    public GameBranchData? BranchData;
    public System.Threading.Mutex Mutex = new();

    public static LauncherGameInfoData? GameInfo;
    public const int GameImagesBackground = 0;
    public const int GameImagesLogo = 1;
    public const int GameImagesThumbnail = 2;
    public const int GameImagesIcon = 3;

    public static async Task FetchGameInfo()
    {
        if (GameInfo == null)
        {
            GameInfo = new();
            using var hc = new System.Net.Http.HttpClient();
            var ok = false;
            try
            {
                var resp = (await hc.GetFromJsonAsync<Response<LauncherGameInfoData>>(
                    $"h!t!t!p!s!:!/!/!s!g!-!h!y!p!-!a!p!i!.!h!o!y!o!v!e!r!s!e!.!c!o!m!/!h!y!p!/!h!y!p!-!c!o!n!n!e!c!t!/!a!p!i!/!g!e!t!G!a!m!e!s!?!l!a!u!n!c!h!e!r!_!i!d!=!{LauncherId}!".Replace("!", ""),
                    new JsonSerializerOptions() { IncludeFields = true }
                ))!;
                GameInfo = resp.Data!;
                foreach (var game in GameInfo!.Games!)
                {
                    if (!SettingsData.GameSettings.ContainsKey(game.Id!))
                        SettingsData.GameSettings.Add(game.Id!, new SophonGameSettings());
                }
                ok = true;
            }
            finally
            {
                if (!ok)
                    GameInfo = null;
            }
        }
    }

    public override async Task<bool> Update(CancellationTokenSource cancellationToken)
    {
        await HandleDiffGameFiles(cancellationToken);
        return true;
    }

    public override async Task<bool> Delete(CancellationTokenSource cancellationToken)
    {
        Status = new()
        {
            Message = "Initializing...",
        };
        State = GameState.Deleting;
        try
        {
            EnsureGameDirectory(/*false*/);
            await RetrieveApiKeys();
            var (gameManifest, _) = await LoadManifest("game");
            var chunks = gameManifest.Chunks!.Data!;
            foreach (var audioRegion in ((SophonGameSettings)GameSettings).AudioRegions)
            {
                var (manifest, _) = await LoadManifest(audioRegion);
                chunks.AddRange(manifest.Chunks!.Data!);
            }
            {
                var gProg = new GameProgress()
                {
                    Message = "Deleting Files...",
                };
                gProg.Rate.Start();
                Status = gProg;
                gProg.TargetProgress.Add(chunks.Count + 1);
                var chunkTasks = new List<Task>();
                var dirs = new HashSet<string>();
                {
                    var fpath = $"{GameDirectory}/config.ini";
                    dirs.Add(Directory.Exists(fpath) ? fpath : fpath[0..fpath.LastIndexOf('/')]);
                    if (File.Exists(fpath))
                    {
                        chunkTasks.Add(Task.Run(() =>
                        {
                            File.Delete(fpath);
                            gProg.CurrentProgress.Add(1);
                        }));
                    }
                    else
                        gProg.CurrentProgress.Add(1);
                }
                foreach (var file in chunks)
                {
                    while (chunkTasks.Count > ((SophonGameSettings)GameSettings).DeleteThreads)
                    {
                        for (var c = chunkTasks.Count - 1; c >= 0; c--)
                        {
                            if (chunkTasks[c].IsCompleted)
                                chunkTasks.RemoveAt(c);
                        }
                        await Task.Delay(4);
                    }
                    var fpath = $"{GameDirectory}/{file.Filename}";
                    dirs.Add(Directory.Exists(fpath) ? fpath : fpath[0..fpath.LastIndexOf('/')]);
                    if (File.Exists(fpath))
                    {
                        chunkTasks.Add(Task.Run(() =>
                        {
                            File.Delete(fpath);
                            gProg.CurrentProgress.Add(1);
                        }));
                    }
                    else
                        gProg.CurrentProgress.Add(1);
                }
                foreach (var task in chunkTasks)
                    await task;
                foreach (var dir in dirs)
                {
                    if (Directory.Exists(dir) && Directory.EnumerateFiles(dir).LongCount() - Directory.EnumerateDirectories(dir).LongCount() <= 0)
                        Directory.Delete(dir, true);
                }
                gProg.Rate.Reset();
            }
        }
        finally
        {
            Status = null;
            State = GameState.None;
        }
        return true;
    }

    public override async Task<bool> Repair(CancellationTokenSource cancellationToken)
    {
        await HandleGameFiles(false, cancellationToken);
        return true;
    }

    public override async Task<bool> Install(CancellationTokenSource cancellationToken)
    {
        await HandleGameFiles(true, cancellationToken);
        return true;
    }

    public override string? GetInstalledGameVersion()
    {
        var path = $"{GameDirectory}/config.ini";
        if (File.Exists(path))
        {
            var s = File.ReadAllText(path);
            var f = "game_version=";
            var p = s.Find(f);
            if (p != -1)
            {
                var n = s.Find("\n", p + f.Length);
                if (n != -1)
                    return s[(p + f.Length)..n].Trim();
            }
        }
        return null;
    }

    // function for update
    async Task HandleDiffGameFiles(CancellationTokenSource cancellationToken)
    {
        var gDown = new GameDownload();
        Status = new()
        {
            Message = "Initializing...",
        };
        State = GameState.Updating;
        try
        {
            var installedVersion = GetInstalledGameVersion()!;
            EnsureGameDirectory(/*false*/);
            await RetrieveApiKeys();
            //CreateConfigIniFile(BranchInfo!.Branches!.First().Main!.Tag!);
            var (gamePatchManifest, gamePatchData) = await LoadPatchManifest("game");
            gamePatchManifest.Reader?.Dispose();
            var updateChunks = new List<(DiffInfoReader, GamePatchBranchData.ManifestContainer)>();
            var updateDeleteChunks = new List<(PatchReader<ChunkValue<ChunkValue<DeleteFileReader>>>, GamePatchBranchData.ManifestContainer)>();
            foreach (var chunk in gamePatchManifest.Files?.Data!)
                updateChunks.Add((chunk, gamePatchData));
            foreach (var chunk in gamePatchManifest.DeleteFiles?.Data!)
                updateDeleteChunks.Add((chunk, gamePatchData));
            // todo: check if the installed version is already the latest version
            foreach (var audioRegion in ((SophonGameSettings)GameSettings).AudioRegions)
            {
                var (manifest, patchData) = await LoadPatchManifest(audioRegion);
                manifest.Reader?.Dispose();
                if (manifest.Files != null)
                {
                    foreach (var chunk in manifest.Files.Data!)
                        updateChunks.Add((chunk, patchData));
                }
                if (manifest.DeleteFiles != null)
                {
                    foreach (var chunk in manifest.DeleteFiles.Data!)
                        updateDeleteChunks.Add((chunk, patchData));
                }
            }
            var sChunks = new List<List<(DiffInfoReader, GamePatchBranchData.ManifestContainer, bool)>>();
            {
                for (var i = 0; i < ((SophonGameSettings)GameSettings).DownloadThreads; i++)
                    sChunks.Add([]);
            }
            {
                var gProg = new GameProgress()
                {
                    Message = "Checking Files...",
                };
                gProg.Rate.Start();
                Status = gProg;
                var i = 0;
                gProg.TargetProgress.Add(updateChunks.Count/* + updateDeleteChunks.Count*/);
                var chunkTasks = new List<Task>();
                foreach (var (file, cont) in updateChunks)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;
                    while (chunkTasks.Count > ((SophonGameSettings)GameSettings).CheckThreads)
                    {
                        for (var c = chunkTasks.Count - 1; c >= 0; c--)
                        {
                            if (chunkTasks[c].IsCompleted)
                                chunkTasks.RemoveAt(c);
                        }
                        await Task.Delay(4);
                    }
                    var currentI = i;
                    var fpath = $"{GameDirectory}/{file.Filename}";
                    if (file.Patches != null)
                    {
                        var patchFile = file.Patches!.Data!.Find(file => file.Key == installedVersion);
                        if (patchFile != null)
                        {
                            foreach (var patch in patchFile.PatchInfo!.Data!)
                            {
                                //GD.Print($"{gamePatchData.DiffDownload!.UrlPrefix}/{patch.PatchId}");
                                chunkTasks.Add(Task.Run(() =>
                                {
                                    foreach (var chunkReader in file!.Patches!.Data!)
                                    {
                                        foreach (var patch in chunkReader.PatchInfo!.Data!)
                                            gDown.TargetDownload.Add((long)patch.PatchLength!);
                                    }
                                }));
                            }

                        }
                        // patch is not avaiable for the installed game version, so it probably hasn't changed between game versions
                        sChunks[currentI].Add((file, cont, true));
                    }
                    gProg.CurrentProgress.Add(1);
                    // if file.Patches is null the file is new
                    i = (i + 1) % ((SophonGameSettings)GameSettings).UpdateThreads;
                }
                foreach (var task in chunkTasks)
                    await task;
                gProg.Rate.Reset();
            }
            Status = gDown;
            var tasks = new List<Task>();
            gDown.DownloadedData.Reset();
            gDown.DownloadRate.Timer.Start();
            {
                Status.Message = "Updating...";
                for (var i = 0; i < ((SophonGameSettings)GameSettings).UpdateThreads; i++)
                {
                    var chunk = sChunks[i];
                    tasks.Add(Task.Run(async () =>
                    {
                        var vQueue = new ValueQueue();
                        foreach (var (file, cont, download) in chunk)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return;
                            if (download)
                            {
                                while (vQueue.GetValue() > ((SophonGameSettings)GameSettings).UpdateDecodeThreads - 1)
                                    await Task.Delay(4);
                                //GD.Print($"processing file {file.Filename}");
                                vQueue.Add(1);
                                var _ = DownloadDiffGameFile(cont, file, installedVersion, vQueue, cancellationToken);
                            }
                        }
                        while (vQueue.GetValue() > 0)
                            await Task.Delay(4);
                    }));
                }
            }
            foreach (var task in tasks)
                await task;
            gDown.DownloadRate.Timer.Stop();
            if (!cancellationToken.IsCancellationRequested)
                CreateConfigIniFile(BranchInfo!.Branches!.First().Main!.Tag!);
        }
        finally
        {
            Status = null;
            State = GameState.None;
        }
    }

    async Task HandleGameFiles(bool install, CancellationTokenSource cancellationToken)
    {
        var gDown = new GameDownload();
        Status = new()
        {
            Message = "Initializing...",
        };
        State = install ? GameState.Installing : GameState.Repairing;
        try
        {
            EnsureGameDirectory(/*install*/);
            await RetrieveApiKeys();
            var (gameManifest, mcont) = await LoadManifest("game");
            var chunks = new List<(FileReader, GameBranchData.ManifestContainer)>();
            foreach (var chunk in gameManifest.Chunks!.Data!)
                chunks.Add((chunk, mcont));
            foreach (var audioRegion in ((SophonGameSettings)GameSettings).AudioRegions)
            {
                var (manifest, acont) = await LoadManifest(audioRegion);
                foreach (var chunk in manifest.Chunks!.Data!)
                    chunks.Add((chunk, acont));
            }
            var sChunks = new List<List<(FileReader, GameBranchData.ManifestContainer, bool)>>();
            {
                for (var i = 0; i < ((SophonGameSettings)GameSettings).DownloadThreads; i++)
                    sChunks.Add([]);
            }
            {
                var gProg = new GameProgress()
                {
                    Message = "Checking Files...",
                };
                gProg.Rate.Start();
                Status = gProg;
                var i = 0;
                gProg.TargetProgress.Add(chunks.Count);
                var chunkTasks = new List<Task>();
                foreach (var (file, cont) in chunks)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        foreach (var task in chunkTasks)
                            await task;
                        return;
                    }
                    while (chunkTasks.Count > ((SophonGameSettings)GameSettings).CheckThreads)
                    {
                        for (var c = chunkTasks.Count - 1; c >= 0; c--)
                        {
                            if (chunkTasks[c].IsCompleted)
                                chunkTasks.RemoveAt(c);
                        }
                        await Task.Delay(4);
                    }
                    var currentI = i;
                    var fpath = $"{GameDirectory}/{file.Filename}";
                    var fileExists = File.Exists(fpath);
                    var isHashOk = true;
                    if ((file.Flags & 0x40) == 0x40) // folder flag
                        Directory.CreateDirectory(fpath);
                    else
                    {
                        if (fileExists)
                        {
                            chunkTasks.Add(Task.Run(async () =>
                            {
                                if (cancellationToken.IsCancellationRequested)
                                    return;
                                {
                                    using var data = File.OpenRead(fpath);
                                    using var md5 = MD5.Create();
                                    var hash = Convert.ToHexStringLower(await md5.ComputeHashAsync(data));
                                    gProg.Rate.Add(data.Length);
                                    isHashOk = hash == file.Md5;
                                }
                                foreach (var chunkReader in file!.Chunks!.Data!)
                                {

                                    if ((file.Flags & 0x40) != 0x40)
                                    {
                                        if (isHashOk)
                                        {
                                            gDown.DownloadedData.Add((long)chunkReader.UncompressedSize!);
                                            gDown.IgnoreProgress.Add((long)chunkReader.UncompressedSize!);
                                        }
                                        else
                                        {
                                            sChunks[currentI].Add((file, cont, true));
                                            i = (i + 1) % ((SophonGameSettings)GameSettings).DownloadThreads;
                                        }
                                        gDown.TargetDownload.Add((long)chunkReader.UncompressedSize!);
                                    }
                                    else
                                    {
                                        sChunks[currentI].Add((file, cont, true));
                                        i = (i + 1) % ((SophonGameSettings)GameSettings).DownloadThreads;
                                    }
                                }
                                gProg.CurrentProgress.Add(1);
                            }));

                        }
                        else
                        {
                            foreach (var chunkReader in file!.Chunks!.Data!)
                                gDown.TargetDownload.Add((long)chunkReader.UncompressedSize!);
                            sChunks[currentI].Add((file, cont, true));
                            gProg.CurrentProgress.Add(1);
                            i = (i + 1) % ((SophonGameSettings)GameSettings).DownloadThreads;
                        }
                    }
                }
                foreach (var task in chunkTasks)
                    await task;
                gProg.Rate.Reset();
            }
            Status = gDown;
            var tasks = new List<Task>();
            gDown.DownloadRate.Timer.Start();
            {
                Status.Message = install ? "Downloading..." : "Repairing...";
                var vQueue = new ValueQueue();
                for (var i = 0; i < ((SophonGameSettings)GameSettings).DownloadThreads; i++)
                {
                    var currentI = i;
                    var chunk = sChunks[i];
                    tasks.Add(Task.Run(async () =>
                    {
                        var innerTasks = new List<Task>();
                        foreach (var (file, cont, download) in chunk)
                        {
                            while (vQueue.GetValue() > ((SophonGameSettings)GameSettings).DownloadDecodeThreads - 1)
                                await Task.Delay(4);
                            if (cancellationToken.IsCancellationRequested)
                                return;
                            //GD.Print($"processing file {file.Filename}");
                            if (download)
                            {
                                vQueue.Add(1);
                                var _ = DownloadGameFile(cont, file, vQueue, cancellationToken);
                            }

                        }
                        while (vQueue.GetValue() > 0)
                            await Task.Delay(4);
                    }));
                }
            }
            foreach (var task in tasks)
                await task;
            if (!cancellationToken.IsCancellationRequested)
                CreateConfigIniFile(BranchInfo!.Branches!.First().Main!.Tag!);
            gDown.DownloadRate.Timer.Stop();
        }
        finally
        {
            Status = null;
            State = GameState.None;
        }
    }

    public void CreateConfigIniFile(string version)
    {
        var content = GameRegion switch
        {
            GlobalGameRegion => $"[!G!e!n!e!r!a!l!]!\r!\n!c!h!a!n!n!e!l!=!1!\r!\n!c!p!s!=!m!i!h!o!y!o!\r!\n!g!a!m!e!_!v!e!r!s!i!o!n!=!{version}!\r!\n!s!d!k!_!v!e!r!s!i!o!n!=!\r!\n!s!u!b!_!c!h!a!n!n!e!l!=!0!\r!\n!",
            ChinaGameRegion => $"[!G!e!n!e!r!a!l!]!\r!\n!c!h!a!n!n!e!l!=!1!\r!\n!c!p!s!=!m!i!h!o!y!o!\r!\n!g!a!m!e!_!v!e!r!s!i!o!n!=!{version}!\r!\n!s!d!k!_!v!e!r!s!i!o!n!=!\r!\n!s!u!b!_!c!h!a!n!n!e!l!=!1\r!\n!",
            BiliBiliGameRegion => $"[!G!e!n!e!r!a!l!]!\r!\n!c!h!a!n!n!e!l!=!1!4!\r!\n!c!p!s!=!b!i!l!i!b!i!l!i!\r!\n!g!a!m!e!_!v!e!r!s!i!o!n!=!{version}!\r!\n!s!d!k!_!v!e!r!s!i!o!n!=!\r!\n!s!u!b!_!c!h!a!n!n!e!l!=!0!\r!\n!",
            _ => throw new NotSupportedException($"unsupported game region: {GameRegion}"),
        };
        {
            using var f = Godot.FileAccess.Open($"{GameDirectory}/config.ini", Godot.FileAccess.ModeFlags.Write);
            f.StoreString(content.Replace("!", ""));
            f.Close();
        }
    }

    public async Task RetrieveApiKeys()
    {
        string url, tail;
        switch (GameRegion)
        {
            case GlobalGameRegion:
                url = $"h!t!t!p!s!:!/!/!s!g!-!h!y!p!-!a!p!i!.!h!o!y!o!v!e!r!s!e!.!c!o!m!/!h!y!p!/!h!y!p!-!c!o!n!n!e!c!t!/!a!p!i!".Replace("!", "");
                tail = $"g!a!m!e!_!i!d!s![!]!=!{GameId}&!l!a!u!n!c!h!e!r!_!i!d!=!{LauncherId}".Replace("!", "");
                break;
            default:
                throw new Exception("unsupported region");
        }
        using var hc = new System.Net.Http.HttpClient();
        if (BranchInfo == null)
        {
            var resp = (await hc.GetFromJsonAsync<Response<GameInfoBranchData>>(
                $"{url}!/!g!e!t!G!a!m!e!B!r!a!n!c!h!e!s!?!{tail}!".Replace("!", ""), new JsonSerializerOptions() { IncludeFields = true }
            ))!;
            BranchInfo = resp.Data;
            //GD.Print($"fetched version {BranchInfo!.Branches!.First().Main!.Tag}");
        }
    }
    public async Task<Response<T>?> GetGameBranchData<T>(bool installing)
        where T : RecvData, new()
    {
        var targetApi = installing ? "g!e!t!B!u!i!l!d!" : "g!e!t!P!a!t!c!h!B!u!i!l!d!";
        var url = installing ?
            GameRegion switch
            {
                GlobalGameRegion => "s!g!-!p!u!b!l!i!c!-!a!p!i!.!h!o!y!o!v!e!r!s!e!.!c!o!m!",
                ChinaGameRegion => "a!p!i!-!t!a!k!u!m!i!.!m!i!h!o!y!o!.!c!o!m!",
                _ => throw new Exception("unknown game region"),
            } :
            GameRegion switch
            {
                GlobalGameRegion => "s!g!-!d!o!w!n!l!o!a!d!e!r!-!a!p!i!.!h!o!y!o!v!e!r!s!e!.!c!o!m!",
                _ => throw new Exception("unknown game region"),
            };
        var mainBranch = BranchInfo!.Branches!.First().Main!;
        url = $"h!t!t!p!s!:!/!/!{url}!/!d!o!w!n!l!o!a!d!e!r!/!s!o!p!h!o!n!_!c!h!u!n!k!/!a!p!i!/!{targetApi}!?!b!r!a!n!c!h!=!{mainBranch.Branch}!&!p!a!c!k!a!g!e!_!i!d!=!{mainBranch.PackageId}!&!p!a!s!s!w!o!r!d!=!{mainBranch.Password}".Replace("!", "");
        //GD.Print(url);
        using var hc = new System.Net.Http.HttpClient();
        var content = installing ?
            await hc.GetFromJsonAsync<Response<T>>(url, new JsonSerializerOptions() { IncludeFields = true }) :
            // so apparently i used `JsonContent.Create(new List<object>())` (on the now `null` value) and it made the server return an error, oops
            await (await hc.PostAsync(url, null)).Content.ReadFromJsonAsync<Response<T>>(new JsonSerializerOptions() { IncludeFields = true });
        return content;
    }

    public async Task<(ManifestReader, GameBranchData.ManifestContainer)> LoadManifest(string category)
    {
        return await SelectCategory((await GetGameBranchData<GameBranchData>(true))!, category);
    }

    public async Task<(DiffManifestReader, GamePatchBranchData.ManifestContainer)> LoadPatchManifest(string category)
    {
        return await SelectPatchCategory((await GetGameBranchData<GamePatchBranchData>(false))!, category);
    }

    public async Task<(DiffManifestReader, GamePatchBranchData.ManifestContainer)> SelectPatchCategory(Response<GamePatchBranchData> mainData, string categoryName)
    {
        var data = mainData.Data!;
        var category = mainData.Data!.Manifests!.Find(manifest => manifest.MatchingField == categoryName);
        //GD.Print($"obtained category {category}");
        var fname = category!.Manifest!.Id;
        var url = $"{category.ManifestDownload!.UrlPrefix!}/{fname}";
        //GD.Print(url);
        using var hc = new System.Net.Http.HttpClient();
        var dataOut = await hc.GetByteArrayAsync(url);
        if (category.ManifestDownload!.Compression! >= 1)
        {
            using var decomp = new Decompressor();
            dataOut = decomp.Unwrap(dataOut).ToArray();
        }
        /*{
            using var f = File.Create(fname!);
            f.Write(dataOut);
            f.Close();
        }*/
        var d = new DiffManifestReader()
        {
            Reader = new BinaryReader(new MemoryStream(dataOut)),
        };
        d.Read();
        d.Reader.Dispose();
        return (d, category);
    }

    public async Task<(ManifestReader, GameBranchData.ManifestContainer)> SelectCategory(Response<GameBranchData> mainData, string categoryName)
    {
        var data = mainData.Data!;
        var category = mainData.Data!.Manifests!.Find(manifest => manifest.MatchingField == categoryName);
        //GD.Print($"obtained category {category}");
        var fname = category!.Manifest!.Id;
        var url = $"{category.ManifestDownload!.UrlPrefix!}/{fname}";
        //GD.Print(url);
        using var hc = new System.Net.Http.HttpClient();
        var dataOut = await hc.GetByteArrayAsync(url);
        if (category.ManifestDownload!.Compression! >= 1)
        {
            using var decomp = new Decompressor();
            dataOut = decomp.Unwrap(dataOut).ToArray();
        }
        var d = new ManifestReader()
        {
            Reader = new BinaryReader(new MemoryStream(dataOut)),
        };
        d.Read();
        d.Reader.Dispose();
        return (d, category);
    }

    public static FileReader? FindChunkByFilename(ManifestReader manifest, string filename)
    {
        return manifest.Chunks!.Data!.Find(file => file.Filename == filename);
    }

    public async Task DownloadDiffGameFile(GamePatchBranchData.ManifestContainer category, DiffInfoReader file, string gameVersion, ValueQueue vQueue, CancellationTokenSource cancellationToken)
    {
        //var alreadyMarked = false;
        try
        {
            if (file.Filename!.Contains("..") && !file.Filename.StartsWith('/'))
                throw new NotSupportedException($"unsupported filename: {file.Filename}");
            if (cancellationToken.IsCancellationRequested)
                return;
            var fname = $"{GameDirectory}/{file.Filename}";
            Directory.CreateDirectory(fname[0..fname.LastIndexOf('/')]);
            var patchIndex = file.Patches!.Data!.FindIndex(file => file.Key == gameVersion);
            if (patchIndex == -1)
                return; // patch not found for this version
            var patch = file.Patches.Data[patchIndex];
            if (patch.PatchInfo!.Data!.Count > 1)
                throw new NotSupportedException();
            var chunk = patch.PatchInfo.Data.First();
            using var data = new MemoryStream();
            {
                //GD.Print($"downloading url: {category.DiffDownload!.UrlPrefix!}/{chunk.PatchId!}");
                //data = await hc.GetByteArrayAsync($"{category.DiffDownload!.UrlPrefix!}/{chunk.PatchId!}");
                using var hc = new System.Net.Http.HttpClient();
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{category.DiffDownload!.UrlPrefix!}/{chunk.PatchId!}");
                req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(chunk.PatchOffset ?? 0, (chunk.PatchOffset ?? 0) + chunk.PatchLength!);
                using HttpResponseMessage? resp = await hc.SendAsync(req);
                if (resp != null)
                {
                    using var stream = await resp.Content.ReadAsStreamAsync();
                    var buffer = new byte[0xffff];
                    while (true)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;
                        var read = await stream.ReadAsync(buffer);
                        if (read <= 0)
                            break;
                        else
                        {
                            data.Write(buffer.AsSpan()[..read]);
                            if (Status is GameDownload gDown)
                            {
                                gDown.DownloadRate.Add(read);
                                gDown.DownloadedData.Add(read);
                            }
                        }
                    }
                    /*alreadyMarked = true;
                    vQueue.Remove(1);*/
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;
                        await File.WriteAllBytesAsync($"{fname}-{patchIndex}.hdiff", data.ToArray());
                    }
                }
            }

            // probably should task this or something
            var isHdiff = true;
            {
                var patcher = new HDiffPatch();
                try
                {
                    // for new files OriginalSize, OriginalMd5, Name & PatchOffset will be null.
                    // UnknownNumber is not, no idea what that is
                    patcher.Initialize($"{fname}-{patchIndex}.hdiff");
                }
                catch (FormatException)
                {
                    //GD.Print($"{ex}\nFILE: {fname}-{patchIndex}.hdiff");
                    isHdiff = false;
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    File.Delete($"{fname}-{patchIndex}.hdiff");
                    return;
                }
                if (isHdiff)
                    patcher.Patch(fname, $"{fname}-{patchIndex}.out", true, default, false, true);
            }
            if (!isHdiff)
                File.Move($"{fname}-{patchIndex}.hdiff", fname, true);
            else
            {
                File.Move($"{fname}-{patchIndex}.out", fname, true);
                File.Delete($"{fname}-{patchIndex}.hdiff");
            }

            //GD.Print($"downloaded file {file.Filename}");
        }
        finally
        {
            //if (!alreadyMarked)
            vQueue.Remove(1);
        }
    }

    public async Task DownloadGameFile(GameBranchData.ManifestContainer category, FileReader file, ValueQueue vQueue, CancellationTokenSource cancellationToken)
    {
        try
        {
            if ((file.Flags & 0x40) == 0x40) // directory
            {
                Directory.CreateDirectory($"{GameDirectory}/{file.Filename}");
                if (cancellationToken.IsCancellationRequested)
                    return;
            }
            else
            {
                if (file.Filename!.Contains("..") && !file.Filename.StartsWith('/'))
                    throw new NotSupportedException($"unsupported filename: {file.Filename}");
                if (cancellationToken.IsCancellationRequested)
                    return;
                using var hc = new System.Net.Http.HttpClient();
                var tasks = new List<Task>();
                var fname = $"{GameDirectory}/{file.Filename}";
                Directory.CreateDirectory(fname[0..fname.LastIndexOf('/')]);
                using var f = File.Create(fname);
                using var m = new System.Threading.Mutex();
                var offset = 0L;
                for (var i = 0; i < file.Chunks!.Data!.Count; i++)
                {
                    var chunk = file.Chunks.Data[i];
                    var data = new MemoryStream();
                    {
                        using HttpResponseMessage? resp = await hc.SendAsync(new(HttpMethod.Get, $"{category.ChunkDownload!.UrlPrefix!}/{chunk.ChunkId!}"));
                        using var stream = await resp.Content.ReadAsStreamAsync();
                        var buffer = new byte[0xffff];
                        while (true)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return;
                            var read = await stream.ReadAsync(buffer);
                            if (read <= 0)
                                break;
                            else
                            {
                                data.Write(buffer.AsSpan()[..read]);
                                if (Status is GameDownload gDown)
                                {
                                    gDown.DownloadRate.Add(read);
                                    gDown.DownloadedData.Add(read);
                                }
                            }
                        }
                    }
                    var currentOffset = offset;
                    offset += (long)chunk.UncompressedSize!;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            using var decomp = new Decompressor();
                            var decompData = decomp.Unwrap(data.ToArray());
                            m.WaitOne();
                            try
                            {
                                f.Seek(currentOffset, SeekOrigin.Begin);
                                f.Write(decompData);
                            }
                            finally
                            {
                                m.ReleaseMutex();
                            }
                        }
                        finally
                        {
                            await data.DisposeAsync();
                        }
                    }));
                }
                foreach (var task in tasks)
                    await task;
                f.Close();
                //GD.Print($"downloaded file {file.Filename}");
            }
        }
        finally
        {
            vQueue.Remove(1);
        }
    }
}