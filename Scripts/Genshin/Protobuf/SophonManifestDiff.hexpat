#pragma array_limit 0xffffffff
#pragma pattern_limit 0xffffffff

import std.sys;
import type.leb128;

const u32 DiffManifest_Files = 10;
const u32 DiffManifest_DeleteFiles = 18;

const u32 DiffFileInfo_Filename = 10;
const u32 DiffFileInfo_Size = 16;
const u32 DiffFileInfo_Md5 = 26;
const u32 DiffFileInfo_Patches = 34;

const u32 Patch_Key = 10;
const u32 Patch_Info = 18;

const u32 PatchInfo_PatchId = 10;
const u32 PatchInfo_Tag = 18;
const u32 PatchInfo_BuildId = 26;
const u32 PatchInfo_UnknownNumber = 32;
const u32 PatchInfo_PatchMd5 = 42;
const u32 PatchInfo_PatchOffset = 48;
const u32 PatchInfo_PatchLength = 56;
const u32 PatchInfo_Name = 66;
const u32 PatchInfo_OriginalSize = 72;
const u32 PatchInfo_OriginalMd5 = 82;

const u32 DeleteFile_Key = 10;
const u32 DeleteFile_Info = 18;

const u32 DeleteFileInfo_Filename = 10;
const u32 DeleteFileInfo_Size = 16;
const u32 DeleteFileInfo_Md5 = 26;

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
                return addr + 1;
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
            ("DiffFileInfo"): {
                match (read_tag) {
                    (DiffFileInfo_Filename | DiffFileInfo_Md5): {
                        type::LEB128 num @ addr;
                        addr += sizeof(num) + num;
                    }
                    (DiffFileInfo_Size): {
                        type::LEB128 num @ addr;
                        addr += sizeof(num);
                    }
                    (DiffFileInfo_Patches): {
                        type::LEB128 size @ addr;
                        addr += sizeof(size);
                        s128 out_addr = addr_get_tag_addr(addr, -1, "Patch", -1);
                        if (out_addr != -1)
                            addr = out_addr;
                    }
                    (_): {
                        //std::print("reading unknown value at {}", addr - 1);
                        Unknown ukn @ addr - 1;
                        addr += sizeof(ukn) - 1;
                        //std::print("finished on addr {} with unknown value size {}", addr, sizeof(ukn));
                    }
                }
            }
            ("Patch"): {
                match (read_tag) {
                    (Patch_Key): {
                        type::LEB128 num @ addr;
                        addr += sizeof(num) + num;
                    }
                    (Patch_Info): {
                        type::LEB128 size @ addr;
                        addr += sizeof(size);
                        s128 out_addr = addr_get_tag_addr(addr, -1, "PatchInfo", -1);
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
            ("PatchInfo"): {
                match (read_tag) {
                    (PatchInfo_PatchId | PatchInfo_Tag | PatchInfo_BuildId | PatchInfo_PatchMd5 | PatchInfo_Name | PatchInfo_OriginalMd5): {
                        type::LEB128 num @ addr;
                        addr += sizeof(num) + num;
                    }
                    (PatchInfo_UnknownNumber | PatchInfo_PatchOffset | PatchInfo_PatchLength | PatchInfo_OriginalSize): {
                        type::LEB128 num @ addr;
                        addr += sizeof(num);
                    }
                    (_): {
                        //std::print("reading unknown value at {}", addr - 1);
                        Unknown ukn @ addr - 1;
                        addr += sizeof(ukn) - 1;
                        //std::print("finished on addr {} with unknown value size {}", addr, sizeof(ukn) - 1);
                    }
                }
            }
            ("DeleteFileInfo"): {
                match (read_tag) {
                    (DeleteFileInfo_Filename | DeleteFileInfo_Md5): {
                        type::LEB128 num @ addr;
                        addr += sizeof(num) + num;
                    }
                    (DeleteFileInfo_Size): {
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
            ("DeleteFile"): {
                match (read_tag) {
                    (DeleteFile_Key): {
                        type::LEB128 num @ addr;
                        addr += sizeof(num) + num;
                    }
                    (DeleteFile_Info): {
                        type::LEB128 size @ addr;
                        addr += sizeof(size);
                        s128 out_addr = addr_get_tag_addr(addr, -1, "DeleteFileInfo", -1);
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

s128 patchinfo_limit = -1;

struct PatchInfo {
    s128 tag_addr = get_tag_addr(PatchInfo_PatchId, "PatchInfo", patchinfo_limit);
    if (tag_addr != -1)
        String patch_id @ tag_addr;
    tag_addr = get_tag_addr(PatchInfo_Tag, "PatchInfo", patchinfo_limit);
    if (tag_addr != -1)
        String tag @ tag_addr;
    tag_addr = get_tag_addr(PatchInfo_BuildId, "PatchInfo", patchinfo_limit);
    if (tag_addr != -1)
        String build_id @ tag_addr;

    tag_addr = get_tag_addr(PatchInfo_UnknownNumber, "PatchInfo", patchinfo_limit);
    if (tag_addr != -1)
        type::LEB128 unknown_number @ tag_addr;
    tag_addr = get_tag_addr(PatchInfo_PatchMd5, "PatchInfo", patchinfo_limit);
    if (tag_addr != -1)
        String patch_md5 @ tag_addr;
    tag_addr = get_tag_addr(PatchInfo_PatchOffset, "PatchInfo", patchinfo_limit);
    if (tag_addr != -1)
        type::LEB128 patch_offset @ tag_addr;
        tag_addr = get_tag_addr(PatchInfo_PatchLength, "PatchInfo", patchinfo_limit);
    if (tag_addr != -1)
        type::LEB128 patch_length @ tag_addr;

    tag_addr = get_tag_addr(PatchInfo_Name, "PatchInfo", patchinfo_limit);
    if (tag_addr != -1)
        String name @ tag_addr;
    tag_addr = get_tag_addr(PatchInfo_OriginalSize, "PatchInfo", patchinfo_limit);
    if (tag_addr != -1)
        type::LEB128 original_size @ tag_addr;
    tag_addr = get_tag_addr(PatchInfo_OriginalMd5, "PatchInfo", patchinfo_limit);
    if (tag_addr != -1)
        String original_md5 @ tag_addr;
};

s128 patch_limit = -1;

struct Patch {
    s128 tag_addr = get_tag_addr(Patch_Key, "Patch", patch_limit);
    if (tag_addr != -1)
        String key @ tag_addr;
    tag_addr = get_tag_addr(Patch_Info, "Patch", patch_limit);
    if (tag_addr != -1) {
        type::LEB128 size @ tag_addr;
        u128 start_addr = tag_addr + sizeof(size);
        patchinfo_limit = start_addr + size;
        PatchInfo info @ start_addr;
        $ = patchinfo_limit;
    }
};

s128 difffileinfo_limit = -1;
s128 difffileinfo_pos = -1;

fn get_tag_addr_difffileinfo_pos(s16 tag, str structure, s128 limit) {
    s128 value = get_tag_addr(tag, structure, limit);
    difffileinfo_pos = value;
    return value;
};

struct DiffFileInfo_Patch_Handle {
    if (difffileinfo_pos != -1) {
        type::LEB128 size @ difffileinfo_pos;
        u128 start_addr = difffileinfo_pos + sizeof(size);
        patch_limit = start_addr + size;
        Patch file @ start_addr;
        $ = patch_limit;
    }
};

struct DiffFileInfo {
    s128 tag_addr = get_tag_addr(DiffFileInfo_Filename, "DiffFileInfo", difffileinfo_limit);
    if (tag_addr != -1)
        String filename @ tag_addr;

    tag_addr = get_tag_addr(DiffFileInfo_Size, "DiffFileInfo", difffileinfo_limit);
    if (tag_addr != -1)
        type::LEB128 size @ tag_addr;
    tag_addr = get_tag_addr(DiffFileInfo_Md5, "DiffFileInfo", difffileinfo_limit);
    if (tag_addr != -1)
        String md5 @ tag_addr;

    tag_addr = get_tag_addr(DiffFileInfo_Patches, "DiffFileInfo", difffileinfo_limit);
    if (tag_addr != -1)
        DiffFileInfo_Patch_Handle patches[while(get_tag_addr_difffileinfo_pos(DiffFileInfo_Patches, "DiffFileInfo", difffileinfo_limit) != -1)] @ tag_addr - 1;
};

s128 deletefileinfo_limit = -1;

struct DeleteFileInfo {
    s128 tag_addr = get_tag_addr(DeleteFileInfo_Filename, "DeleteFileInfo", deletefileinfo_limit);
    if (tag_addr != -1)
        String filename @ tag_addr;

    tag_addr = get_tag_addr(DeleteFileInfo_Size, "DeleteFileInfo", deletefileinfo_limit);
    if (tag_addr != -1)
        type::LEB128 size @ tag_addr;
    tag_addr = get_tag_addr(DeleteFileInfo_Md5, "DeleteFileInfo", deletefileinfo_limit);
    if (tag_addr != -1)
        String md5 @ tag_addr;
};

s128 deletefile_limit = -1;
s128 deletefile_pos = -1;
s128 deletefile_fpos = -1;

fn get_tag_addr_deletefile_pos(s16 tag, str structure, s128 limit) {
    s128 value = get_tag_addr(tag, structure, limit);
    deletefile_pos = value;
    return value;
};

fn get_tag_addr_deletefile_fpos(s16 tag, str structure, s128 limit) {
    s128 value = get_tag_addr(tag, structure, limit);
    deletefile_fpos = value;
    return value;
};

struct DeleteFile_DeleteFileInfo_Handle {
    if (deletefile_fpos != -1) {
        type::LEB128 size @ deletefile_fpos;
        u128 start_addr = deletefile_fpos + sizeof(size);
        deletefileinfo_limit = start_addr + size;
        DeleteFileInfo file @ start_addr;
        $ = deletefileinfo_limit;
    }
};

struct DeleteFile_DeleteFileInfoList_Handle {
    if (deletefile_pos != -1) {
        type::LEB128 size @ deletefile_pos;
        u128 start_addr = deletefile_pos + sizeof(size);
        deletefile_limit = start_addr + size;
        DeleteFile_DeleteFileInfo_Handle file[while(get_tag_addr_deletefile_fpos(DeleteFileInfo_Filename, "DeleteFile", deletefile_limit) != -1)] @ start_addr;
        $ = deletefile_limit;
    }
};

struct DeleteFile {
    s128 tag_addr = get_tag_addr(DeleteFile_Key, "DeleteFile", deletefile_limit);
    if (tag_addr != -1)
        String key @ tag_addr;
    tag_addr = get_tag_addr(DeleteFile_Info, "DeleteFile", deletefile_limit);
    if (tag_addr != -1) {
        $ = tag_addr - 1;
        DeleteFile_DeleteFileInfoList_Handle info[while(get_tag_addr_deletefile_pos(DeleteFile_Info, "DeleteFile", deletefile_limit) != -1)];
    }
};

fn get_leb128_value() {
    type::LEB128 value @ $;
    return value;
};

bool diffmanifest_files_valid = true;

struct DiffManifest_Files_Handle {
    //s128 tag_addr = get_tag_addr(DiffManifest_Files, "DiffManifest", -1);
    u32 tag = get_leb128_value(); // this fixes a silly bug that i'm lazy to debug atm
    if (tag == DiffManifest_Files) {
        s128 tag_addr = $ + 1;
        type::LEB128 size @ tag_addr;
        u128 start_addr = tag_addr + sizeof(size);
        difffileinfo_limit = start_addr + size;
        DiffFileInfo file @ start_addr;
        $ = difffileinfo_limit;
    } else
        diffmanifest_files_valid = false;
};

bool diffmanifest_deletefiles_valid = true;

struct DiffManifest_DeleteFiles_Handle {
    //s128 tag_addr = get_tag_addr(DiffManifest_DeleteFiles, "DiffManifest", -1);
    u32 tag = get_leb128_value(); // this fixes a silly bug that i'm lazy to debug atm
    if (tag == DiffManifest_DeleteFiles) {
        s128 tag_addr = $ + 1;
        type::LEB128 size @ tag_addr;
        u128 start_addr = tag_addr + sizeof(size);
        deletefile_limit = start_addr + size;
        DeleteFile file @ start_addr;
        $ = deletefile_limit;
    } else
        diffmanifest_deletefiles_valid = false;
};

struct DiffManifest {
    DiffManifest_Files_Handle files[while(diffmanifest_files_valid)];
    DiffManifest_DeleteFiles_Handle delete_files[while(diffmanifest_deletefiles_valid)];
};

DiffManifest manifest @ 0x00;