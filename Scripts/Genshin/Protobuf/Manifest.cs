namespace Genshin.Protobuf.Manifest;

using global::Protobuf;

public class ChunkReader : ProtobufBaseReader
{
    enum Tag : int
    {
        ChunkId = 10,
        Md5 = 18,
        Offset = 24,
        CompressedSize = 32,
        UncompressedSize = 40,
        Xxhash = 48,
        UnknownHash = 58,
    }

    public string? ChunkId;
    public string? Md5;
    public long? Offset;
    public long? CompressedSize;
    public long? UncompressedSize;
    public long? Xxhash;

    public override void ReadValue(int tag)
    {
        switch ((Tag)tag)
        {
            case Tag.ChunkId:
                ChunkId = Reader!.ReadString();
                break;
            case Tag.Md5:
                Md5 = Reader!.ReadString();
                break;
            case Tag.Offset:
                Offset = Reader!.Read7BitEncodedInt64();
                break;
            case Tag.CompressedSize:
                CompressedSize = Reader!.Read7BitEncodedInt64();
                break;
            case Tag.UncompressedSize:
                UncompressedSize = Reader!.Read7BitEncodedInt64();
                break;
            case Tag.Xxhash:
                Xxhash = Reader!.Read7BitEncodedInt64();
                break;
        }
    }

    public override void Read()
    {
        for (var i = 0; i < 6 && Reader!.BaseStream.Position < GetSizeLimit(); i++)
            ReadValue(Reader.Read7BitEncodedInt());
    }
}

public class FileReader : ProtobufBaseReader
{
    enum Tag : int
    {
        Filename = 10,
        Chunks = 18,
        Flags = 24,
        Size = 32,
        Md5 = 42,
    }

    public string? Filename;
    public ChunkValue<ChunkReader>? Chunks;
    public long? Flags;
    public long? Size;
    public string? Md5;

    public override void ReadValue(int tag)
    {
        switch ((Tag)tag)
        {
            case Tag.Filename:
                Filename = Reader!.ReadString();
                break;
            case Tag.Chunks:
                Chunks = new()
                {
                    Reader = Reader,
                };
                Chunks.ReadValue(tag);
                break;
            case Tag.Flags:
                Flags = Reader!.Read7BitEncodedInt64();
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
        for (var i = 0; i < 5 && Reader!.BaseStream.Position < GetSizeLimit(); i++)
            ReadValue(Reader!.Read7BitEncodedInt());
    }
}

public class ManifestReader : ProtobufBaseReader
{
    enum Tag : int
    {
        Chunks = 10,
    }

    public ChunkValue<FileReader>? Chunks;

    public override void ReadValue(int tag)
    {
        switch ((Tag)tag)
        {
            case Tag.Chunks:
                Chunks = new()
                {
                    Reader = Reader,
                };
                Chunks.ReadValue(tag);
                break;
        }
    }

    public override void Read()
    {
        ReadValue(Reader!.Read7BitEncodedInt());
    }
}