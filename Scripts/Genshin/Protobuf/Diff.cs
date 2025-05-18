namespace Genshin.Protobuf.Diff;

using global::Protobuf;

public class DiffInfoReader : ProtobufBaseReader
{
    public enum Tag : int
    {
        Filename = 10,
        Size = 16,
        Md5 = 26,
        Patches = 34,
    }

    public string? Filename;
    public long? Size;
    public string? Md5;
    public ChunkValue<PatchReader<ChunkValue<PatchInfoReader>>>? Patches;

    public override void ReadValue(int tag)
    {
        switch ((Tag)tag)
        {
            case Tag.Filename:
                Filename = Reader!.ReadString();
                break;
            case Tag.Size:
                Size = Reader!.Read7BitEncodedInt64();
                break;
            case Tag.Md5:
                Md5 = Reader!.ReadString();
                break;
            case Tag.Patches:
                Patches = new()
                {
                    Reader = Reader,
                };
                Patches.ReadValue(tag);
                break;
        }
    }

    public override void Read()
    {
        for (var i = 0; i < 4 && Reader!.BaseStream.Position < GetSizeLimit(); i++)
            ReadValue(Reader!.Read7BitEncodedInt());
    }
}

public class PatchReader<T> : ProtobufBaseReader
    where T : ProtobufBaseReader, new()
{
    public enum Tag : int
    {
        Key = 10,
        PatchInfo = 18,
    }

    public string? Key;
    public T? PatchInfo;

    public override void ReadValue(int tag)
    {
        switch ((Tag)tag)
        {
            case Tag.Key:
                Key = Reader!.ReadString();
                break;
            case Tag.PatchInfo:
                PatchInfo = new()
                {
                    Reader = Reader,
                };
                PatchInfo.ReadValue(tag);
                break;
        }
    }

    public override void Read()
    {
        for (var i = 0; i < 2 && Reader!.BaseStream.Position < GetSizeLimit(); i++)
            ReadValue(Reader!.Read7BitEncodedInt());
    }
}

public class PatchInfoReader : ProtobufBaseReader
{
    public enum Tag : int
    {
        PatchId = 10,
        VersionTag = 18,
        BuildId = 26,
        UnknownNumber = 32,
        PatchMd5 = 42,
        PatchOffset = 48,
        PatchLength = 56,
        Name = 66,
        OriginalSize = 72,
        OriginalMd5 = 82,
    }

    public string? PatchId; // Probably Xxhash_Md5?
    public string? VersionTag;
    public string? BuildId;

    public long? UnknownNumber;
    public string? PatchMd5;
    public long? PatchOffset;
    public long? PatchLength;

    public string? Name;
    public long? OriginalSize;
    public string? OriginalMd5;

    public override void ReadValue(int tag)
    {
        switch ((Tag)tag)
        {
            case Tag.PatchId:
                PatchId = Reader!.ReadString();
                break;
            case Tag.VersionTag:
                VersionTag = Reader!.ReadString();
                break;
            case Tag.BuildId:
                BuildId = Reader!.ReadString();
                break;
            case Tag.UnknownNumber:
                UnknownNumber = Reader!.Read7BitEncodedInt64();
                break;
            case Tag.PatchMd5:
                PatchMd5 = Reader!.ReadString();
                break;
            case Tag.PatchOffset:
                PatchOffset = Reader!.Read7BitEncodedInt64();
                break;
            case Tag.PatchLength:
                PatchLength = Reader!.Read7BitEncodedInt64();
                break;
            case Tag.Name:
                Name = Reader!.ReadString();
                break;
            case Tag.OriginalSize:
                OriginalSize = Reader!.Read7BitEncodedInt64();
                break;
            case Tag.OriginalMd5:
                OriginalMd5 = Reader!.ReadString();
                break;
        }
    }

    public override void Read()
    {
        for (var i = 0; i < 10 && Reader!.BaseStream.Position < GetSizeLimit(); i++)
            ReadValue(Reader.Read7BitEncodedInt());
    }
}

public class DeleteFileReader : ProtobufBaseReader
{
    public enum Tag : int
    {
        Filename = 10,
        Size = 16,
        Md5 = 26,
    }

    public string? Filename;
    public long? Size;
    public string? Md5;

    public override void ReadValue(int tag)
    {
        switch ((Tag)tag)
        {
            case Tag.Filename:
                Filename = Reader!.ReadString();
                break;
            case Tag.Size:
                Size = Reader!.Read7BitEncodedInt64();
                break;
            case Tag.Md5:
                Md5 = Reader!.ReadString();
                break;
        }
    }

    public override void Read()
    {
        for (var i = 0; i < 3 && Reader!.BaseStream.Position < GetSizeLimit(); i++)
            ReadValue(Reader!.Read7BitEncodedInt());
    }
}

public class DiffManifestReader : ProtobufBaseReader
{
    public enum Tag : int
    {
        Files = 10,
        DeleteFiles = 18,
    }

    public ChunkValue<DiffInfoReader>? Files;
    public ChunkValue<PatchReader<ChunkValue<ChunkValue<DeleteFileReader>>>>? DeleteFiles;

    public override void ReadValue(int tag)
    {
        switch ((Tag)tag)
        {
            case Tag.Files:
                Files = new()
                {
                    Reader = Reader,
                };
                Files.ReadValue(tag);
                break;
            case Tag.DeleteFiles:
                DeleteFiles = new()
                {
                    Reader = Reader,
                };
                DeleteFiles.ReadValue(tag);
                break;
        }
    }

    public override void Read()
    {
        for (var i = 0; i < 2 && Reader!.BaseStream.Position < GetSizeLimit(); i++)
            ReadValue(Reader!.Read7BitEncodedInt());
    }
}