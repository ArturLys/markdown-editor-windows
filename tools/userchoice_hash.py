"""Compute the Windows UserChoice hash for a file-extension default.
Usage: userchoice_hash.py <ext> <sid> <progid> <filetime-hex>
Prints the base64 hash. The registry key must be written in the same
minute the filetime refers to (Windows validates against LastWriteTime).
"""
import base64, hashlib, struct, sys

M = 0xFFFFFFFF
USER_EXPERIENCE = "User Choice set via Windows User Experience {D18B6DD5-6124-4341-9318-804CB8E20EB1}"


def compute(ext: str, sid: str, progid: str, ft_hex: str) -> str:
    base = (ext + sid + progid + ft_hex + USER_EXPERIENCE).lower()
    data = base.encode("utf-16-le") + b"\x00\x00"
    md5 = hashlib.md5(data).digest()
    md51 = struct.unpack("<I", md5[0:4])[0] | 1
    md52 = struct.unpack("<I", md5[4:8])[0] | 1

    length_base = len(data)
    length = int((length_base & 4) <= 1) + (length_base // 4) - 1
    if length <= 1:
        return ""
    chunks = ((length - 2) >> 1) + 1

    def dword(i):
        return struct.unpack("<I", data[i:i + 4])[0]

    # pass 1 (uses offset MD5 words)
    md51p = (md51 + 0x69FB0000) & M
    md52p = (md52 + 0x13DB0000) & M
    o1 = o2 = cache = 0
    p = 0
    for _ in range(chunks):
        r0 = (dword(p) + o1) & M
        r1 = dword(p + 4)
        p += 8
        r2a = (r0 * md51p - 0x10FA9605 * (r0 >> 16)) & M
        r2b = (0x79F8A395 * r2a + 0x689B6B9F * (r2a >> 16)) & M
        r3 = (0xEA970001 * r2b - 0x3C101569 * (r2b >> 16)) & M
        r4 = (r3 + r1) & M
        r5 = (cache + r3) & M
        r6a = (r4 * md52p - 0x3CE8EC25 * (r4 >> 16)) & M
        r6b = (0x59C3AF2D * r6a - 0x2232E0F1 * (r6a >> 16)) & M
        o1 = (0x1EC90001 * r6b + 0x35BD1EC9 * (r6b >> 16)) & M
        o2 = (r5 + o1) & M
        cache = o2
    h1a, h2a = o1, o2

    # pass 2
    o1 = o2 = cache = 0
    p = 0
    for _ in range(chunks):
        r0 = (dword(p) + o1) & M
        b = dword(p + 4)
        p += 8
        r1a = (r0 * md51) & M
        r1b = (0xB1110000 * r1a - 0x30674EEF * (r1a >> 16)) & M
        r2a = (0x5B9F0000 * r1b - 0x78F7A461 * (r1b >> 16)) & M
        r2b = (0x12CEB96D * (r2a >> 16) - 0x46930000 * r2a) & M
        r3 = (0x1D830000 * r2b + 0x257E1D83 * (r2b >> 16)) & M
        r4a = (md52 * ((r3 + b) & M)) & M
        r4b = (0x16F50000 * r4a - 0x5D8BE90B * (r4a >> 16)) & M
        r5a = (0x96FF0000 * r4b - 0x2C7C6901 * (r4b >> 16)) & M
        r5b = (0x2B890000 * r5a + 0x7C932B89 * (r5a >> 16)) & M
        o1 = (0x9F690000 * r5b - 0x405B6097 * (r5b >> 16)) & M
        o2 = (o1 + cache + r3) & M
        cache = o2
    h1b, h2b = o1, o2

    out = struct.pack("<II", h1a ^ h1b, h2a ^ h2b)
    return base64.b64encode(out).decode()


if __name__ == "__main__":
    print(compute(*sys.argv[1:5]))
