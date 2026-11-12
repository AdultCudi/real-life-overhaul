using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>One shader a bundle holds: its name as the engine finds it, and the graphics APIs it was compiled for.</summary>
internal sealed class BundleShader
{
    public long PathId;
    public string Name;
    public string Fallback;
    public readonly List<int> Platforms = new List<int>(2);   // the engine's shader compiler platforms: 4 Direct3D 11, 14 Metal, 18 Vulkan
}

/// <summary>One material a bundle holds, and the shader it points at: one of the bundle's own, or one outside it.</summary>
internal sealed class BundleMaterial
{
    public string Name;
    public int ShaderFile;      // 0: in this bundle
    public long ShaderPath;
}

/// <summary>What a shipped asset bundle holds that a graphics API decides, read from the file itself.</summary>
internal sealed class BundleContents
{
    public string Engine;                 // "2022.3.62f2"
    public int Target = -1;               // the build target it was made for: 19 is 64-bit Windows, 2 macOS
    public string Problem;                // why it could not be read, or null
    public readonly Dictionary<int, int> Kinds = new Dictionary<int, int>();   // objects by class: 48 Shader, 21 Material, 28 Texture2D, 43 Mesh, 74 AnimationClip
    public readonly List<BundleShader> Shaders = new List<BundleShader>(2);
    public readonly List<BundleMaterial> Materials = new List<BundleMaterial>(4);
}

/// <summary>
/// Reads a Unity asset bundle of the mod's (UnityFS, built in chunks with LZ4, with type trees, as every builder of the
/// mod's makes them) far enough to list its shaders and materials: the bundle's header and blocks, each file's object
/// table, and those objects by their own type trees. Nothing of Unity is needed, so the check reads what ships.
/// </summary>
internal static class BundleFile
{
    private const int ShaderClass = 48, MaterialClass = 21;

    public static BundleContents Read(string path)
    {
        var found = new BundleContents();
        try
        {
            ReadInto(File.ReadAllBytes(path), found);
        }
        catch (Exception e)
        {
            found.Problem = e.GetType().Name + ": " + e.Message;
        }
        return found;
    }

    // ---------------------------------------------------------------- the bundle

    private static void ReadInto(byte[] file, BundleContents found)
    {
        var r = new Cursor(file, true);
        if (r.Text() != "UnityFS")
        {
            throw new InvalidDataException("not a UnityFS bundle");
        }
        uint version = r.U32();
        r.Text();                           // the player's version
        found.Engine = r.Text();
        r.I64();                            // the file's size
        int packed = (int)r.U32(), unpacked = (int)r.U32();
        uint flags = r.U32();
        if (version >= 7)
        {
            r.Align(16);
        }
        byte[] info;
        if ((flags & 0x80) != 0)
        {
            info = Unpack(file, file.Length - packed, packed, unpacked, (int)(flags & 0x3F));   // the blocks' list at the end of the file
        }
        else
        {
            info = Unpack(file, r.At, packed, unpacked, (int)(flags & 0x3F));
            r.At += packed;
        }
        if ((flags & 0x200) != 0)
        {
            r.Align(16);
        }
        var b = new Cursor(info, true);
        b.At = 16;                          // a hash
        int blocks = b.I32();
        var sizes = new int[blocks];
        var packedSizes = new int[blocks];
        var kinds = new int[blocks];
        long total = 0;
        for (int i = 0; i < blocks; i++)
        {
            sizes[i] = (int)b.U32();
            packedSizes[i] = (int)b.U32();
            kinds[i] = b.U16() & 0x3F;
            total += sizes[i];
        }
        var data = new byte[total];
        int at = r.At, to = 0;
        for (int i = 0; i < blocks; i++)
        {
            byte[] block = Unpack(file, at, packedSizes[i], sizes[i], kinds[i]);
            Buffer.BlockCopy(block, 0, data, to, sizes[i]);
            at += packedSizes[i];
            to += sizes[i];
        }
        int nodes = b.I32();
        for (int i = 0; i < nodes; i++)
        {
            long offset = b.I64(), size = b.I64();
            b.U32();
            string name = b.Text();
            if (name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
            {
                continue;   // the textures' and sounds' own bytes: no objects in them
            }
            ReadFile(data, (int)offset, (int)size, found);
        }
    }

    private static byte[] Unpack(byte[] from, int at, int packed, int unpacked, int kind)
    {
        var result = new byte[unpacked];
        switch (kind)
        {
            case 0:
                Buffer.BlockCopy(from, at, result, 0, unpacked);
                return result;
            case 2:
            case 3:
                Lz4(from, at, packed, result);
                return result;
            default:
                throw new InvalidDataException("the bundle is packed with LZMA (kind " + kind + "): build it in chunks (LZ4), as the mod's builders do");
        }
    }

    // LZ4's block format: a token of two lengths, the literals, a two-byte distance back, the match.
    private static void Lz4(byte[] from, int at, int length, byte[] into)
    {
        int end = at + length, to = 0;
        while (at < end)
        {
            int token = from[at++];
            int literals = token >> 4;
            if (literals == 15)
            {
                int more;
                do
                {
                    more = from[at++];
                    literals += more;
                }
                while (more == 255);
            }
            Buffer.BlockCopy(from, at, into, to, literals);
            at += literals;
            to += literals;
            if (at >= end)
            {
                break;   // the last sequence is literals alone
            }
            int back = from[at] | from[at + 1] << 8;
            at += 2;
            int match = token & 15;
            if (match == 15)
            {
                int more;
                do
                {
                    more = from[at++];
                    match += more;
                }
                while (more == 255);
            }
            match += 4;
            int src = to - back;
            if (back <= 0 || src < 0)
            {
                throw new InvalidDataException("a damaged LZ4 block");
            }
            for (int k = 0; k < match; k++)
            {
                into[to++] = into[src++];   // byte by byte: a match may run into what it writes
            }
        }
        if (to != into.Length)
        {
            throw new InvalidDataException("an LZ4 block unpacked to " + to + " bytes, not " + into.Length);
        }
    }

    // ---------------------------------------------------------------- one serialized file of it

    private sealed class Node
    {
        public int Level, Size, Meta;
        public bool Array;
        public string Type, Name;
    }

    private static void ReadFile(byte[] data, int start, int size, BundleContents found)
    {
        var h = new Cursor(data, true) { At = start };
        h.U32();
        h.U32();
        uint version = h.U32();
        long dataOffset = h.U32();
        if (version < 22)
        {
            throw new InvalidDataException("a serialized file of version " + version + ": this reader knows Unity 2020 and later (22)");
        }
        bool big = h.U8() != 0;
        h.At += 3;
        h.U32();
        h.I64();
        dataOffset = h.I64();
        h.I64();
        var r = new Cursor(data, big) { At = h.At };
        r.Text();                           // the engine's version again
        found.Target = r.I32();
        bool trees = r.U8() != 0;
        int typeCount = r.I32();
        var classes = new int[typeCount];
        var types = new List<Node>[typeCount];
        for (int t = 0; t < typeCount; t++)
        {
            classes[t] = r.I32();
            r.U8();                         // stripped
            r.I16();                        // its script's index
            if (classes[t] == 114)
            {
                r.At += 16;                 // a script's own hash
            }
            r.At += 16;                     // the type's hash
            if (!trees)
            {
                continue;
            }
            int nodeCount = r.I32(), strings = r.I32();
            int nodesAt = r.At, stringsAt = nodesAt + nodeCount * 32;
            var list = new List<Node>(nodeCount);
            for (int n = 0; n < nodeCount; n++)
            {
                var c = new Cursor(data, big) { At = nodesAt + n * 32 };
                c.U16();
                var node = new Node { Level = c.U8() };
                node.Array = (c.U8() & 1) != 0;
                uint typeAt = c.U32(), nameAt = c.U32();
                node.Size = c.I32();
                c.I32();
                node.Meta = c.I32();
                node.Type = Word(data, stringsAt, strings, typeAt);
                node.Name = Word(data, stringsAt, strings, nameAt);
                list.Add(node);
            }
            types[t] = list;
            r.At = stringsAt + strings;
            int depends = r.I32();
            r.At += depends * 4;
        }
        int objects = r.I32();
        for (int o = 0; o < objects; o++)
        {
            r.Align(4, start);
            long pathId = r.I64();
            long byteStart = r.I64();
            int byteSize = (int)r.U32();
            int type = r.I32();
            int cls = type >= 0 && type < typeCount ? classes[type] : -1;
            found.Kinds[cls] = found.Kinds.TryGetValue(cls, out int had) ? had + 1 : 1;
            if (cls != ShaderClass && cls != MaterialClass)
            {
                continue;
            }
            if (types[type] == null)
            {
                throw new InvalidDataException("the bundle was built without type trees, so its shaders cannot be named");
            }
            var at = new Cursor(data, big) { At = start + (int)dataOffset + (int)byteStart };
            int objectStart = at.At;
            if (cls == ShaderClass)
            {
                var shader = new BundleShader { PathId = pathId };
                int index = 0;
                Walk(types[type], ref index, at, objectStart, string.Empty, (path, node, c) =>
                {
                    if (path == "/m_ParsedForm/m_Name")
                    {
                        shader.Name = Peek(c);
                    }
                    else if (path == "/m_ParsedForm/m_FallbackName")
                    {
                        shader.Fallback = Peek(c);
                    }
                    else if (path == "/platforms/Array/data")
                    {
                        shader.Platforms.Add((int)new Cursor(c.Data, c.Big) { At = c.At }.U32());
                    }
                });
                found.Shaders.Add(shader);
            }
            else
            {
                var material = new BundleMaterial();
                int index = 0;
                Walk(types[type], ref index, at, objectStart, string.Empty, (path, node, c) =>
                {
                    if (path == "/m_Name")
                    {
                        material.Name = Peek(c);
                    }
                    else if (path == "/m_Shader/m_FileID")
                    {
                        material.ShaderFile = new Cursor(c.Data, c.Big) { At = c.At }.I32();
                    }
                    else if (path == "/m_Shader/m_PathID")
                    {
                        material.ShaderPath = new Cursor(c.Data, c.Big) { At = c.At }.I64();
                    }
                });
                found.Materials.Add(material);
            }
        }
    }

    // A string as the engine writes one: its length, then its bytes.
    private static string Peek(Cursor at)
    {
        var c = new Cursor(at.Data, at.Big) { At = at.At };
        int length = c.I32();
        return Encoding.UTF8.GetString(c.Data, c.At, length);
    }

    // Walks one node of a type tree and everything under it, moving the cursor past its bytes; seen is told each node with
    // its path ("/m_ParsedForm/m_Name") and the cursor at its first byte.
    private static void Walk(List<Node> nodes, ref int index, Cursor at, int objectStart, string path, Action<string, Node, Cursor> seen)
    {
        Node node = nodes[index];
        int level = node.Level;
        string here = level == 0 ? string.Empty : path + "/" + node.Name;
        seen(here, node, at);
        index++;
        bool children = index < nodes.Count && nodes[index].Level > level;
        if (node.Array)
        {
            // An array: its size, then that many of its one element, whose nodes follow the size's.
            int count = at.I32();
            int sizeNode = index;
            index++;                         // the size
            int element = index;
            int after = element;
            Skip(nodes, ref after);
            Node first = nodes[element];
            bool plain = after == element + 1 && first.Size > 0 && (first.Meta & 0x4000) == 0;
            if (plain && here != "/platforms/Array")
            {
                at.At += count * first.Size;   // bytes, numbers: passed over whole
            }
            else
            {
                for (int k = 0; k < count; k++)
                {
                    int each = element;
                    Walk(nodes, ref each, at, objectStart, here, seen);
                }
            }
            index = after;
            _ = sizeNode;
        }
        else if (children)
        {
            while (index < nodes.Count && nodes[index].Level > level)
            {
                Walk(nodes, ref index, at, objectStart, here, seen);
            }
        }
        else
        {
            at.At += node.Size;
        }
        if ((node.Meta & 0x4000) != 0)
        {
            at.Align(4, objectStart);
        }
    }

    // Moves the index past a node and everything under it, reading nothing.
    private static void Skip(List<Node> nodes, ref int index)
    {
        int level = nodes[index].Level;
        index++;
        while (index < nodes.Count && nodes[index].Level > level)
        {
            index++;
        }
    }

    // A type tree's word: from its own strings, or by its place in the engine's list of common ones.
    private static string Word(byte[] data, int stringsAt, int strings, uint offset)
    {
        if ((offset & 0x80000000) == 0)
        {
            int at = stringsAt + (int)offset, end = at;
            while (end < stringsAt + strings && data[end] != 0)
            {
                end++;
            }
            return Encoding.UTF8.GetString(data, at, end - at);
        }
        switch (offset & 0x7FFFFFFF)
        {
            case 49: return "Array";
            case 106: return "data";
            case 427: return "m_Name";
            default: return "#" + (offset & 0x7FFFFFFF);   // a common word no path this reader looks for is made of
        }
    }

    /// <summary>A place in a byte array, read in either byte order.</summary>
    private sealed class Cursor
    {
        public readonly byte[] Data;
        public readonly bool Big;
        public int At;

        public Cursor(byte[] data, bool big)
        {
            Data = data;
            Big = big;
        }

        public int U8() => Data[At++];

        public int U16()
        {
            int v = Big ? Data[At] << 8 | Data[At + 1] : Data[At] | Data[At + 1] << 8;
            At += 2;
            return v;
        }

        public int I16() => (short)U16();

        public uint U32()
        {
            uint v = Big
                ? (uint)(Data[At] << 24 | Data[At + 1] << 16 | Data[At + 2] << 8 | Data[At + 3])
                : (uint)(Data[At] | Data[At + 1] << 8 | Data[At + 2] << 16 | Data[At + 3] << 24);
            At += 4;
            return v;
        }

        public int I32() => (int)U32();

        public long I64()
        {
            long a = U32(), b = U32();
            return Big ? a << 32 | b : b << 32 | a;
        }

        public string Text()
        {
            int end = At;
            while (Data[end] != 0)
            {
                end++;
            }
            string s = Encoding.UTF8.GetString(Data, At, end - At);
            At = end + 1;
            return s;
        }

        public void Align(int to, int from = 0)
        {
            int rest = (At - from) % to;
            if (rest != 0)
            {
                At += to - rest;
            }
        }
    }
}
