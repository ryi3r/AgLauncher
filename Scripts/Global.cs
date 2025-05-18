using System.Collections.Generic;
using Godot;

public static class Global
{
    public static string? SystemWineVersion;
    public static WineComponents? WineComponents;
    public static Dictionary<string, List<WineData>> WineData = [];
    public static Dictionary<string, List<DxvkData>> DxvkData = [];
    /// dict<gameid, list<Image?>>
    public static Dictionary<string, List<Image?>> GameImages = [];
}