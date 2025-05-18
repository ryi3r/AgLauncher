namespace Protobuf;

using System.Collections.Generic;
using System.IO;

public abstract class ProtobufBaseReader
{
    public BinaryReader? Reader;
    public long? SizeLimit;

    public long GetSizeLimit()
    {
        return SizeLimit != null ? (long)SizeLimit : (Reader != null ? Reader.BaseStream.Length : -1);
    }

    /// <returns>reader position after reading the value</returns>
    public virtual long ReadValueAt(int tag, long position)
    {
        var curPosition = Reader!.BaseStream.Position;
        ReadValue(tag);
        var finalPosition = Reader.BaseStream.Position;
        Reader.BaseStream.Position = curPosition;
        return finalPosition;
    }
    public abstract void ReadValue(int tag);
    /// <returns>reader position after reading the value</returns>
    public virtual long ReadAt(long position)
    {
        var curPosition = Reader!.BaseStream.Position;
        Read();
        var finalPosition = Reader.BaseStream.Position;
        Reader.BaseStream.Position = curPosition;
        return finalPosition;
    }
    public abstract void Read();

}

public class ChunkValue<T> : ProtobufBaseReader
    where T : ProtobufBaseReader, new()
{
    public List<T>? Data;
    public override void ReadValue(int targetTag)
    {
        Data = [];
        var tag = targetTag;
        var lastPosition = Reader!.BaseStream.Position;
        while (tag == targetTag && Reader.BaseStream.Position < GetSizeLimit())
        {
            var valueSize = Reader.Read7BitEncodedInt64();
            var startAddr = Reader.BaseStream.Position;
            var endAddr = startAddr + valueSize;
            var value = new T()
            {
                Reader = Reader,
                SizeLimit = endAddr,
            };
            Data.Add(value);
            value.Read();
            lastPosition = endAddr;
            if (endAddr >= GetSizeLimit())
                break;
            Reader.BaseStream.Position = endAddr;
            tag = Reader.Read7BitEncodedInt();
        }
        Reader.BaseStream.Position = lastPosition;
    }
    public override void Read()
    {
        ReadValue(Reader!.Read7BitEncodedInt());
    }
}

public class UnknownReader : ProtobufBaseReader
{
    enum Tag : int
    {
        Leb128 = 0,
        Fixed64 = 1,
        Blob = 2,
        StartGroup = 3,
        EndGroup = 4,
        Fixed32 = 5,
    }

    public long? Number;
    public ulong? UnsignedNumber;
    public byte[]? Blob;
    public ChunkValue<UnknownReader>? Data;


    public override void ReadValue(int tag)
    {
        switch ((Tag)tag)
        {
            case Tag.Leb128:
                Number = Reader!.Read7BitEncodedInt64();
                break;
            case Tag.Fixed32:
                UnsignedNumber = Reader!.ReadUInt32();
                break;
            case Tag.Fixed64:
                UnsignedNumber = Reader!.ReadUInt64();
                break;
            case Tag.Blob:
                Blob = Reader!.ReadBytes(Reader.Read7BitEncodedInt());
                break;
            case Tag.StartGroup:
                Data = new()
                {
                    Reader = Reader,
                };
                Data.ReadValue(tag);
                break;
            case Tag.EndGroup:
                throw new System.NotImplementedException();
        }
    }

    public override void Read()
    {
        ReadValue(Reader!.Read7BitEncodedInt() & 7);
    }
}
