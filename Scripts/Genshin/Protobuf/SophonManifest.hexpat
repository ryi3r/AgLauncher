#pragma array_limit 0xffffffff
#pragma pattern_limit 0xffffffff

import std.sys;
import type.leb128;

const u32 ChunkInfo_ChunkId = 10;
const u32 ChunkInfo_Md5 = 18;
const u32 ChunkInfo_Offset = 24;
const u32 ChunkInfo_CompressedSize = 32;
const u32 ChunkInfo_UncompressedSize = 40;
const u32 ChunkInfo_Xxhash = 48;
const u32 ChunkInfo_UnknownHash = 58;

const u32 FileInfo_Filename = 10;
const u32 FileInfo_Chunks = 18;
const u32 FileInfo_Flags = 24;
const u32 FileInfo_Size = 32;
const u32 FileInfo_Md5 = 42;

const u32 Unknown_Leb128 = 0;
const u32 Unknown_u64 = 1;
const u32 Unknown_Length = 2;
const u32 Unknown_StartGroup = 3;
const u32 Unknown_EndGroup = 4;
const u32 Unknown_u32 = 5;

struct String {
    type::LEB128 size;
    char data[size];
} [[static, std::string(data)]];

struct Unknown {
    type::LEB128 tag;
    u128 size = 0;
    // https://github.com/protocolbuffers/protobuf/blob/73a5afe278170c1771993cedea6694a2700569b8/csharp/src/Google.Protobuf/WireFormat.cs#L55
    u32 ptag = tag & 7;
    match (ptag) {
        (Unknown_Leb128): {
            type::LEB128 number;
            size = sizeof(number);
        }
        (Unknown_u32): {
            u32 number;
            size = 4;
        }
        (Unknown_u64): {
            u64 number;
            size = 8;
        }
        (Unknown_Length): {
            type::LEB128 length;
            $ += length;
            //std::mem::Bytes<length> data;
            size = sizeof(length) + length;
        }
        (Unknown_StartGroup): {
            type::LEB128 length;
            Unknown data[length];
            size = sizeof(length) + sizeof(data);
        }
        (Unknown_EndGroup): {
            break;
        }
        (_): {
            std::print("unknown tagwire {} ({})", tag, ptag);
        }
    }
};

// returns address of tag start (will always be after the tag), returns -1 if not found
// if tag is -1 it will search for the end tag instead
fn get_tag_addr(s16 tag, str structure, s128 limit) {
    return addr_get_tag_addr($, tag, structure, limit);
};

fn addr_get_tag_addr(s128 addr, s16 tag, str structure, s128 limit) {
    //std::print("searcing for tag {} on structure {} starting at addr {}", tag, structure, addr);
    while (true) {
        if ((limit != -1 && addr >= limit) || addr >= std::mem::size()) {
            //std::print("file end reached addr:{} limit:{} fsize:{} flag1:{} flag2:{}", addr, limit, std::mem::size(), limit != -1 && addr >= limit, addr >= std::mem::size());
            break;
        }
        type::LEB128 read_tag @ addr;
        // end group tag
        if (read_tag & 7 == 4) {
            if (tag == -1) {
                //std::print("found end group tag at addr {}", addr);
                return addr;
            }/* else
                std::print("end group tag reached");*/
            break;
        }
        if (tag != -1 && tag == read_tag) {
            //std::print("found tag {} at addr {}", read_tag, addr);
            return addr + 1;
        }
        //std::print("read tag {} at addr {}", read_tag, addr);
        addr += sizeof(read_tag);
        match (structure) {
            ("ChunkInfo"): {
                match (read_tag) {
                    (ChunkInfo_ChunkId | ChunkInfo_Md5): {
                        type::LEB128 num @ addr;
                        addr += sizeof(num) + num;
                    }
                    (ChunkInfo_Offset | ChunkInfo_Xxhash | ChunkInfo_CompressedSize | ChunkInfo_UncompressedSize): {
                        type::LEB128 num @ addr;
                        addr += sizeof(num);
                    }
                    (_): {
                        //std::print("reading unknown value at {}", addr - 1);
                        Unknown ukn @ addr - 1;
                        addr += sizeof(ukn) - 1;
                        //std::print("finished on addr {} with unknown value size {}", addr, sizeof(ukn));
                    }
                }
            }
            ("FileInfo"): {
                match (read_tag) {
                    (FileInfo_Filename | FileInfo_Md5): {
                        type::LEB128 num @ addr;
                        addr += sizeof(num) + num;
                    }
                    (FileInfo_Flags | FileInfo_Size): {
                        type::LEB128 num @ addr;
                        addr += sizeof(num);
                    }
                    (FileInfo_Chunks): {
                        type::LEB128 size @ addr;
                        addr += sizeof(size);
                        s128 out_addr = addr_get_tag_addr(addr, -1, "ChunkInfo", -1);
                        if (out_addr != -1)
                            addr = out_addr;
                    }
                    (_): {
                        //std::print("reading unknown value at {}", addr - 1);
                        Unknown ukn @ addr - 1;
                        addr += sizeof(ukn) - 1;
                        //std::print("finished on addr {} with unknown value size {}", addr, sizeof(ukn) - 1);
                    }
                }
            }
        }
    }
    return -1;
};

s128 chunkinfo_limit = -1;

struct ChunkInfo {
    s128 tag_addr = get_tag_addr(ChunkInfo_ChunkId, "ChunkInfo", chunkinfo_limit);
    if (tag_addr != -1)
        String chunk_id @ tag_addr;
    tag_addr = get_tag_addr(ChunkInfo_Md5, "ChunkInfo", chunkinfo_limit);
    if (tag_addr != -1)
        String md5 @ tag_addr;

    tag_addr = get_tag_addr(ChunkInfo_Offset, "ChunkInfo", chunkinfo_limit);
    if (tag_addr != -1)
        type::LEB128 offset @ tag_addr;
    tag_addr = get_tag_addr(ChunkInfo_CompressedSize, "ChunkInfo", chunkinfo_limit);
    if (tag_addr != -1)
        type::LEB128 compressed_size @ tag_addr;
    tag_addr = get_tag_addr(ChunkInfo_UncompressedSize, "ChunkInfo", chunkinfo_limit);
    if (tag_addr != -1)
        type::LEB128 uncompressed_size @ tag_addr;
    tag_addr = get_tag_addr(ChunkInfo_Xxhash, "ChunkInfo", chunkinfo_limit);
    if (tag_addr != -1)
        type::LEB128 xxhash @ tag_addr;
    tag_addr = get_tag_addr(ChunkInfo_UnknownHash, "ChunkInfo", chunkinfo_limit);
    if (tag_addr != -1)
        String unknown_hash @ tag_addr;
};

s128 fileinfo_limit = -1;
s128 fileinfo_pos = -1;

fn get_tag_addr_fileinfo_pos(s16 tag, str structure, s128 limit) {
    s128 value = get_tag_addr(tag, structure, limit);
    fileinfo_pos = value;
    return value;
};

struct FileInfo_Chunks_Handle {
    if (fileinfo_pos != -1) {
        type::LEB128 size @ fileinfo_pos;
        u128 start_addr = fileinfo_pos + sizeof(size);
        chunkinfo_limit = start_addr + size;
        ChunkInfo chunk @ start_addr;
        $ = chunkinfo_limit;
    }
};

struct FileInfo {
    s128 tag_addr = get_tag_addr(FileInfo_Filename, "FileInfo", fileinfo_limit);
    if (tag_addr != -1)
        String filename @ tag_addr;

    FileInfo_Chunks_Handle chunks[while(get_tag_addr_fileinfo_pos(FileInfo_Chunks, "FileInfo", fileinfo_limit) != -1)];

    tag_addr = get_tag_addr(FileInfo_Flags, "FileInfo", fileinfo_limit);
    if (tag_addr != -1)
        type::LEB128 flags @ tag_addr;
    tag_addr = get_tag_addr(FileInfo_Size, "FileInfo", fileinfo_limit);
    if (tag_addr != -1)
        type::LEB128 size @ tag_addr;

    tag_addr = get_tag_addr(FileInfo_Md5, "FileInfo", fileinfo_limit);
    if (tag_addr != -1)
        String md5 @ tag_addr;
};

bool manifest_fileinfo_valid = true;

struct Manifest_FileInfo_Handle {
    s128 tag_addr = get_tag_addr(FileInfo_Filename, "Manifest", -1);
    if (tag_addr != -1) {
        type::LEB128 size @ tag_addr;
        u128 start_addr = tag_addr + sizeof(size);
        fileinfo_limit = start_addr + size;
        FileInfo file @ start_addr;
        $ = fileinfo_limit;
    } else
        manifest_fileinfo_valid = false;
};

struct Manifest {
    Manifest_FileInfo_Handle chunks[while(manifest_fileinfo_valid)];
};

Manifest manifest @ 0x00;
