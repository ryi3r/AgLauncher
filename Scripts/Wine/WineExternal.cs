using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public class WineComponents
{
    public class WineComponent
    {
        public class WineFeatures
        {
            [JsonPropertyName("bundle")] public string? Bundle;
            [JsonPropertyName("need_dxvk")] public bool? NeedDxvk;
            [JsonPropertyName("compact_launch")] public bool? CompactLaunch;
            [JsonPropertyName("command")] public string? Command;
            [JsonPropertyName("env")] public Dictionary<string, JsonElement>? Env;
            [JsonPropertyName("recommended")] public bool? Recommended;
        }
        [JsonPropertyName("name")] public string? Name;
        [JsonPropertyName("title")] public string? Title;
        [JsonPropertyName("features")] public WineFeatures? Features;
    }
    public class DxvkComponent
    {
        public class DxvkFeatures
        {
            [JsonPropertyName("env")] public Dictionary<string, JsonElement>? Env;
            [JsonPropertyName("recommended")] public bool? Recommended;
        }
        [JsonPropertyName("name")] public string? Name;
        [JsonPropertyName("title")] public string? Title;
        [JsonPropertyName("features")] public DxvkFeatures? Features;
    }
    [JsonPropertyName("wine")] public List<WineComponent>? Wine;
    [JsonPropertyName("dxvk")] public List<DxvkComponent>? Dxvk;
}

public class WineData
{
    public class WineFiles
    {
        [JsonPropertyName("wine")] public required string Wine;
        [JsonPropertyName("wine64")] public string? Wine64;
        [JsonPropertyName("wineserver")] public string? WineServer;
        [JsonPropertyName("wineboot")] public string? WineBoot;
    }
    public class WineFeatures
    {
        [JsonPropertyName("bundle")] public string? Bundle;
        [JsonPropertyName("need_dxvk")] public bool? NeedDxvk;
        [JsonPropertyName("compact_launch")] public bool? CompactLaunch;
        [JsonPropertyName("command")] public string? Command;
        [JsonPropertyName("env")] public Dictionary<string, JsonElement>? Env;
        [JsonPropertyName("recommended")] public bool? Recommended;
    }

    [JsonPropertyName("name")] public required string Name;
    [JsonPropertyName("title")] public required string Title;
    [JsonPropertyName("uri")] public required string Uri;
    [JsonPropertyName("format")] public string? Format;
    [JsonPropertyName("files")] public required WineFiles Files;
    [JsonPropertyName("features")] public WineFeatures? Features;
}

public class DxvkData
{
    public class DxvkFeatures
    {
        [JsonPropertyName("env")] public Dictionary<string, JsonElement>? Env;
        [JsonPropertyName("recommended")] public bool? Recommended;
    }

    [JsonPropertyName("name")] public required string Name;
    [JsonPropertyName("title")] public required string Title;
    [JsonPropertyName("uri")] public required string Uri;
    [JsonPropertyName("format")] public string? Format;
    [JsonPropertyName("features")] public DxvkFeatures? Features;
}
