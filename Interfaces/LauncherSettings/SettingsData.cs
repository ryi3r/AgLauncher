using System;
using System.Collections.Generic;
using Godot;

public static class SettingsData
{
    public static string WineRepo = "https://github.com/an-anime-team/components";
    public static string WineType = "ge-proton";
    // (name, version)
    // or if custom:
    // ("custom", path)
    // or if system (wine only):
    // ("system", "")
    public static (string, string) WineVersion = ("ge-proton", "GE-Proton9-23");
    public static (string, string) DxvkVersion = ("vanilla", "dxvk-2.6");
    public static string? InstallationFolder;
    public static bool HasSetup = false;
    public static string? WineFolder;
    public static string? DxvkFolder;
    public static Dictionary<string, GameSettings> GameSettings = [];

    public static string GetComponentsUrl(string subUrl)
    {
        if (!WineRepo.Contains("github.com"))
            throw new NotSupportedException();
        // main -> branch, only supports "main" branch for now, probably should look into supporting more...
        return WineRepo.Replace("github.com", "raw.githubusercontent.com") + $"/refs/heads/main/{subUrl}";
    }

    static bool WriteNullable(FileAccess f, object? obj)
    {
        f.Store8((byte)(obj != null ? 1 : 0));
        return obj != null;
    }
    public static void Save()
    {
        using var f = FileAccess.Open("user://settings.bin", FileAccess.ModeFlags.Write);
        f.Store32(0);

        f.StorePascalString(WineRepo);

        f.StorePascalString(WineVersion.Item1);
        f.StorePascalString(WineVersion.Item2);

        f.StorePascalString(DxvkVersion.Item1);
        f.StorePascalString(DxvkVersion.Item2);

        if (WriteNullable(f, InstallationFolder))
            f.StorePascalString(InstallationFolder);

        if (WriteNullable(f, WineFolder))
            f.StorePascalString(WineFolder);
        if (WriteNullable(f, DxvkFolder))
            f.StorePascalString(DxvkFolder);

        f.Store8((byte)(HasSetup ? 1 : 0));

        f.Store32((uint)GameSettings.Count);
        foreach (var (key, value) in GameSettings)
        {
            f.StorePascalString(key);

            if (WriteNullable(f, value.InstallationFolder))
                f.StorePascalString(value.InstallationFolder);
            f.StorePascalString(value.LaunchScript);

            // idk how to make this better
            if (value is SophonGameSettings sophon)
            {
                f.Store16(1);

                f.Store32((uint)sophon.DownloadThreads);
                f.Store32((uint)sophon.DownloadDecodeThreads);
                f.Store32((uint)sophon.CheckThreads);
                f.Store32((uint)sophon.DeleteThreads);
                f.Store32((uint)sophon.UpdateThreads);
                f.Store32((uint)sophon.UpdateDecodeThreads);

                f.Store32((uint)sophon.AudioRegions.Count);
                foreach (var audioRegion in sophon.AudioRegions)
                    f.StorePascalString(audioRegion);
            }
            else if (value is GameSettings)
                f.Store16(0);
            else
                f.Store16(ushort.MaxValue);
        }

        f.Close();
    }

    public static void Load()
    {
        if (FileAccess.FileExists("user://settings.bin"))
        {
            using var f = FileAccess.Open("user://settings.bin", FileAccess.ModeFlags.Read);
            var version = f.Get32();
            if (version >= 0)
            {
                WineRepo = f.GetPascalString();

                WineVersion.Item1 = f.GetPascalString();
                WineVersion.Item2 = f.GetPascalString();

                DxvkVersion.Item1 = f.GetPascalString();
                DxvkVersion.Item2 = f.GetPascalString();

                InstallationFolder = f.Get8() == 1 ? f.GetPascalString() : null;

                WineFolder = f.Get8() == 1 ? f.GetPascalString() : null;
                DxvkFolder = f.Get8() == 1 ? f.GetPascalString() : null;

                HasSetup = f.Get8() == 1;

                var gameSettingsSize = f.Get32();
                GameSettings.Clear();
                for (var _i = 0; _i < gameSettingsSize; _i++)
                {
                    var key = f.GetPascalString();
                    var instFolder = f.Get8() == 1 ? f.GetPascalString() : null;
                    var launchScript = f.GetPascalString();
                    var type = f.Get16();
                    var value = new GameSettings();
                    if (type == 1)
                    {
                        var sophon = new SophonGameSettings()
                        {
                            DownloadThreads = (int)f.Get32(),
                            DownloadDecodeThreads = (int)f.Get32(),
                            CheckThreads = (int)f.Get32(),
                            DeleteThreads = (int)f.Get32(),
                            UpdateThreads = (int)f.Get32(),
                            UpdateDecodeThreads = (int)f.Get32(),
                        };

                        {
                            var size = f.Get32();
                            for (var i = 0; i < size; i++)
                                sophon.AudioRegions.Add(f.GetPascalString());
                        }

                        value = sophon;
                    }

                    value.InstallationFolder = instFolder;
                    value.LaunchScript = launchScript;
                    GameSettings.Add(key, value);
                }
            }
            f.Close();
        }
    }
}