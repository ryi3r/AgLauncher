using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Base;

public enum GameState
{
    None,
    Running,
    Installing,
    Updating,
    Repairing,
    Deleting,
}

public class GameStatus
{
    public string Message = "";

    public GameStatus() { }
}

public class GameDownload : GameStatus
{
    public TimedQueue DownloadRate = new();
    public ValueQueue DownloadedData = new();
    public ValueQueue TargetDownload = new();
    public ValueQueue IgnoreProgress = new();
    public bool IndeterminateProgressBar = false;
}

public class GameProgress : GameStatus
{
    public TimedQueue Rate = new();
    public ValueQueue CurrentProgress = new();
    public ValueQueue TargetProgress = new();
    public ValueQueue IgnoreProgress = new();
    public bool IndeterminateProgressBar = false;
}

public abstract class BaseGame(string gameDirectory)
{
    public const string GlobalGameRegion = "os";
    public const string ChinaGameRegion = "cn";
    public const string BiliBiliGameRegion = "bb";

    public const string MainBranch = "main";
    public const string PreDownloadBranch = "pre_download";

    public string GameDirectory = gameDirectory;
    public string GameRegion = GlobalGameRegion;
    public string? InstalledVersion;
    public string Branch = MainBranch;
    public GameStatus? Status;
    public GameState State = GameState.None;
    public string GameName = "Unknown";
    public string GameId = "";
    public string TargetVersion = "?.?.?";
    public GameSettings GameSettings = new();

    protected void EnsureGameDirectory(/*bool checkForFiles*/)
    {
        Directory.CreateDirectory(GameDirectory);
        if (!Directory.Exists(GameDirectory))
            throw new NotSupportedException($"{GameDirectory} is not a directory");
        /*else if (checkForFiles && Directory.EnumerateFiles(GameDirectory).Any())
            throw new NotSupportedException($"{GameDirectory} contains files but expected directory not to have any");*/
    }

    public abstract Task<bool> Install(CancellationTokenSource cancellationToken);
    public abstract Task<bool> Update(CancellationTokenSource cancellationToken);
    public abstract Task<bool> Repair(CancellationTokenSource cancellationToken);
    public abstract Task<bool> Delete(CancellationTokenSource cancellationToken);

    public abstract string? GetInstalledGameVersion();
}