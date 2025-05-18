using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Genshin.Sophon;

#region Game Patch/Branch Data
public class GameBranchData : RecvData
{
    public class ManifestContainer
    {
        public class ManifestData
        {
            [JsonPropertyName("id")] public string? Id;
            [JsonPropertyName("checksum")] public string? Checksum;
            [JsonPropertyName("compressed_size")] public string? CompressedSize;
            [JsonPropertyName("uncompressed_size")] public string? UncompressedSize;
        }
        public class Download
        {
            [JsonPropertyName("encryption")] public int? Encryption;
            [JsonPropertyName("password")] public string? Password;
            [JsonPropertyName("compression")] public int? Compression;
            [JsonPropertyName("url_prefix")] public string? UrlPrefix;
            [JsonPropertyName("url_suffix")] public string? UrlSuffix;
        }
        public class Stat
        {
            [JsonPropertyName("compressed_size")] public string? CompressedSize;
            [JsonPropertyName("uncompressed_size")] public string? UncompressedSize;
            [JsonPropertyName("file_count")] public string? FileCount;
            [JsonPropertyName("chunk_count")] public string? ChunkCount;
        }

        [JsonPropertyName("category_id")] public string? CategoryId;
        [JsonPropertyName("category_name")] public string? CategoryName;
        [JsonPropertyName("manifest")] public ManifestData? Manifest;
        [JsonPropertyName("chunk_download")] public Download? ChunkDownload;
        [JsonPropertyName("manifest_download")] public Download? ManifestDownload;
        [JsonPropertyName("matching_field")] public string? MatchingField;
        [JsonPropertyName("stats")] public Stat? Stats;
        [JsonPropertyName("deduplicated_stats")] public Stat? DeduplicatedStats;
    }

    [JsonPropertyName("build_id")] public string? BuildId;
    [JsonPropertyName("patch_id")] public string? PatchId;
    [JsonPropertyName("tag")] public string? Tag;
    [JsonPropertyName("manifests")] public List<ManifestContainer>? Manifests;
}

public class GamePatchBranchData : RecvData
{
    public class ManifestContainer
    {
        [JsonPropertyName("category_id")] public string? CategoryId;
        [JsonPropertyName("category_name")] public string? CategoryName;
        [JsonPropertyName("manifest")] public GameBranchData.ManifestContainer.ManifestData? Manifest;
        [JsonPropertyName("diff_download")] public GameBranchData.ManifestContainer.Download? DiffDownload;
        [JsonPropertyName("manifest_download")] public GameBranchData.ManifestContainer.Download? ManifestDownload;
        [JsonPropertyName("matching_field")] public string? MatchingField;
        [JsonPropertyName("stats")] public Dictionary<string, GameBranchData.ManifestContainer.Stat>? Stats;
    }

    [JsonPropertyName("build_id")] public string? BuildId;
    [JsonPropertyName("patch_id")] public string? PatchId;
    [JsonPropertyName("tag")] public string? Tag;
    [JsonPropertyName("manifests")] public List<ManifestContainer>? Manifests;
}

public class GameInfoBranchData : RecvData
{
    public class Branch
    {
        public class InfoData
        {
            [JsonPropertyName("id")] public string? Id;
            [JsonPropertyName("biz")] public string? Biz;
        }
        public class BranchData
        {
            public class Category
            {
                [JsonPropertyName("category_id")] public string? Id;
                [JsonPropertyName("matching_field")] public string? Field;
            }

            [JsonPropertyName("package_id")] public string? PackageId;
            [JsonPropertyName("branch")] public string? Branch;
            [JsonPropertyName("password")] public string? Password;
            [JsonPropertyName("tag")] public string? Tag;
            [JsonPropertyName("diff_tags")] public List<string>? DiffTags;
            [JsonPropertyName("categories")] public List<Category>? Categories;
        }

        [JsonPropertyName("game")] public InfoData? Game;
        [JsonPropertyName("main")] public BranchData? Main;
        [JsonPropertyName("pre_download")] public BranchData? PreDownload;
    }

    [JsonPropertyName("game_branches")] public List<Branch>? Branches;
}
#endregion

#region Fetch Game Data
public class LauncherGameInfoData : RecvData
{
    public class ImageData
    {
        [JsonPropertyName("url")] public string? Url;
        [JsonPropertyName("hover_url")] public string? HoverUrl;
        [JsonPropertyName("link")] public string? Link;
        [JsonPropertyName("login_state_in_link")] public bool? LoginStateInLink;
        [JsonPropertyName("md5")] public string? Md5;
        [JsonPropertyName("size")] public int? Size;
    }
    public class DisplayData
    {
        [JsonPropertyName("language")] public string? Language;
        [JsonPropertyName("name")] public string? Name;
        [JsonPropertyName("icon")] public ImageData? Icon;
        [JsonPropertyName("title")] public string? Title;
        [JsonPropertyName("subtitle")] public string? Subtitle;
        [JsonPropertyName("background")] public ImageData? Background;
        [JsonPropertyName("logo")] public ImageData? Logo;
        [JsonPropertyName("thumbnail")] public ImageData? Thumbnail;
        [JsonPropertyName("korea_rating")] public string? KoreaRating;
        [JsonPropertyName("shortcut")] public ImageData? Shortcut;
    }
    public class ServerConfig
    {
        [JsonPropertyName("i18n_name")] public string? I18NName;
        [JsonPropertyName("i18n_description")] public string? I18NDescription;
        [JsonPropertyName("package_name")] public string? PackageName;
        [JsonPropertyName("auto_scan_registry_key")] public string? AutoScanRegistryKey;
        [JsonPropertyName("package_detection_info")] public string? PackageDetectionInfo;
        [JsonPropertyName("game_id")] public string? GameId;
        [JsonPropertyName("reservation")] public string? Reservation;
        [JsonPropertyName("display_status")] public string? DisplayStatus;
    }
    public class Game
    {
        [JsonPropertyName("id")] public string? Id;
        [JsonPropertyName("biz")] public string? Biz;
        [JsonPropertyName("display")] public DisplayData? Display;
        [JsonPropertyName("reservation")] public string? Reservation;
        [JsonPropertyName("display_status")] public string? DisplayStatus;
        [JsonPropertyName("game_server_configs")] public List<ServerConfig>? GameServerConfig;
    }
    [JsonPropertyName("games")] public List<Game>? Games;
}
#endregion

public class RecvData
{
    public RecvData() { }
}

public class Response<T>
    where T : RecvData, new()
{
    [JsonPropertyName("retcode")] public int? ReturnCode;
    [JsonPropertyName("message")] public string? Message;
    [JsonPropertyName("data")] public T? Data;
}
