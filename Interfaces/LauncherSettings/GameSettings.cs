using System;
using System.Collections.Generic;
using System.Linq;

public class GameSettings
{
    public string? InstallationFolder;
    public string LaunchScript = "WINEPREFIX=%wineprefix% %wine% %executable%";

    public GameSettings() { }

    public virtual GameSettings Clone()
    {
        return new GameSettings()
        {
            InstallationFolder = InstallationFolder,
        };
    }
}

public class SophonGameSettings : GameSettings
{
    public HashSet<string> AudioRegions = [];
    public int DownloadThreads = 8; // recommended: 8-12
    public int DownloadDecodeThreads = 8; // recommended: 4-8
    public int CheckThreads = 8; // recommended: 8-12
    public int DeleteThreads = 8; // recommended: 8-12
    public int UpdateThreads = 8; // recommended: 4-8
    public int UpdateDecodeThreads = 8; // recommended: 4-8

    public override SophonGameSettings Clone()
    {
        return new SophonGameSettings()
        {
            InstallationFolder = InstallationFolder,

            AudioRegions = [.. AudioRegions],
            DownloadThreads = DownloadThreads,
            DownloadDecodeThreads = DownloadDecodeThreads,
            CheckThreads = CheckThreads,
            DeleteThreads = DeleteThreads,
            UpdateDecodeThreads = UpdateDecodeThreads,
        };
    }
}
