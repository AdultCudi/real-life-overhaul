using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>What a message of the shared city is (Core/SharedCity).</summary>
    public enum SharedKind : byte
    {
        Hello = 1,       // guest to host: this guest is in the world, with its version; the answer is a snapshot
        Snapshot = 2,    // host to one guest: every part of the city the host runs, whole
        Update = 3,      // host to every guest: the parts that changed since the last one
        Request = 4,     // guest to host: something the guest did that changes the shared city
        Refused = 5,     // host to one guest: another version of this mod, so nothing of the city is shared
        Resync = 6       // guest to host: a part came out different here; the answer is a snapshot
    }

    /// <summary>One part of a message: a letter for the system it belongs to and its text (that system's own compact line).</summary>
    public struct SharedPart
    {
        public char Id;
        public string Text;

        public SharedPart(char id, string text)
        {
            Id = id;
            Text = text ?? string.Empty;
        }
    }

    /// <summary>A message of the shared city, as read or to be written.</summary>
    public sealed class SharedMessage
    {
        public SharedKind Kind;
        public string Version = string.Empty;   // the sender's version of this mod
        public int Format = SharedCityWire.Format;
        public int Sequence;                    // counts up from one for each sender's session
        public readonly List<SharedPart> Parts = new List<SharedPart>(6);

        public string Part(char id)
        {
            for (int i = 0; i < Parts.Count; i++)
            {
                if (Parts[i].Id == id)
                {
                    return Parts[i].Text;
                }
            }
            return null;
        }
    }

    /// <summary>What reading a message came to.</summary>
    public enum SharedRead
    {
        Ok,
        Damaged,         // not a whole message of this mod's: nothing of it is used
        OtherFormat,     // a message of this mod's in a layout this version cannot read: only its version is known
        OtherVersion     // a whole message, from another version of this mod: read, but nothing of it is applied
    }

    /// <summary>
    /// The shared city's messages as bytes. Game-free.
    ///
    ///     "RLOZ" | the unpacked length (int) | the packed length (int) | the rest, deflated:
    ///         "RLOC" | format (byte) | kind (byte) | version (string) | sequence (int) | parts (byte) |
    ///         for each: id (byte) | text (string) | CRC-32 of everything before it (uint)
    ///
    /// Strings are as BinaryWriter writes them. The magic, the format and the version come first and never move, so a
    /// later layout can always be told apart and its sender's version named. A message whose CRC, length or layout does
    /// not match is dropped whole. Nothing over <see cref="MostPacked"/> is sent or read, nor unpacks past
    /// <see cref="MostUnpacked"/>. Going Public does not compress its envelopes' attachments, hence the deflate.
    /// </summary>
    public static class SharedCityWire
    {
        /// <summary>Going Public's message type for this mod's messages: free in Going Public (its own end at 222) and in its add-ons (Friend Trade 249, Extras 250).</summary>
        public const byte MessageType = 231;

        public const int Format = 1;
        public const int MostPacked = 1 << 20;        // 1 MB on the wire
        public const int MostUnpacked = 8 << 20;      // 8 MB of text
        public const int MostParts = 16;

        private static readonly byte[] Outer = { (byte)'R', (byte)'L', (byte)'O', (byte)'Z' };
        private static readonly byte[] Inner = { (byte)'R', (byte)'L', (byte)'O', (byte)'C' };
        private static readonly uint[] Table = MakeTable();

        /// <summary>The message as bytes; null if it would be larger than may be sent.</summary>
        public static byte[] Write(SharedMessage m)
        {
            byte[] body;
            using (var raw = new MemoryStream(256))
            {
                using (var w = new BinaryWriter(raw, Encoding.UTF8, true))
                {
                    w.Write(Inner);
                    w.Write((byte)m.Format);
                    w.Write((byte)m.Kind);
                    w.Write(m.Version ?? string.Empty);
                    w.Write(m.Sequence);
                    int count = Math.Min(m.Parts.Count, MostParts);
                    w.Write((byte)count);
                    for (int i = 0; i < count; i++)
                    {
                        w.Write((byte)m.Parts[i].Id);
                        w.Write(m.Parts[i].Text ?? string.Empty);
                    }
                    w.Flush();
                    w.Write(Crc(raw.GetBuffer(), 0, (int)raw.Length));
                }
                body = raw.ToArray();
            }
            if (body.Length > MostUnpacked)
            {
                return null;
            }
            using (var packed = new MemoryStream(body.Length / 3 + 16))
            {
                packed.Write(Outer, 0, 4);
                packed.Write(BitConverter.GetBytes(body.Length), 0, 4);   // little-endian on every machine the game runs on
                packed.Write(BitConverter.GetBytes(0), 0, 4);             // the packed length, once it is known
                using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, true))
                {
                    deflate.Write(body, 0, body.Length);
                }
                if (packed.Length > MostPacked)
                {
                    return null;
                }
                byte[] bytes = packed.ToArray();
                Buffer.BlockCopy(BitConverter.GetBytes(bytes.Length - 12), 0, bytes, 8, 4);
                return bytes;
            }
        }

        /// <summary>
        /// Reads a message: whole with <see cref="SharedRead.OtherVersion"/>, only its version with
        /// <see cref="SharedRead.OtherFormat"/>, null with <see cref="SharedRead.Damaged"/>.
        /// </summary>
        public static SharedRead Read(byte[] bytes, string mine, out SharedMessage m)
        {
            m = null;
            byte[] body = Unpack(bytes);
            if (body == null || body.Length < 4 + 2 + 1 + 4 + 1 + 4)
            {
                return SharedRead.Damaged;
            }
            try
            {
                using (var r = new BinaryReader(new MemoryStream(body, false), Encoding.UTF8))
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (r.ReadByte() != Inner[i])
                        {
                            return SharedRead.Damaged;
                        }
                    }
                    var message = new SharedMessage { Format = r.ReadByte() };
                    byte kind = r.ReadByte();
                    message.Version = r.ReadString();
                    if (message.Format != Format)
                    {
                        // A layout of another version's: the version is all that can be trusted.
                        m = message;
                        return SharedRead.OtherFormat;
                    }
                    if (kind < (byte)SharedKind.Hello || kind > (byte)SharedKind.Resync)
                    {
                        return SharedRead.Damaged;
                    }
                    message.Kind = (SharedKind)kind;
                    message.Sequence = r.ReadInt32();
                    int count = r.ReadByte();
                    if (count > MostParts)
                    {
                        return SharedRead.Damaged;
                    }
                    for (int i = 0; i < count; i++)
                    {
                        char id = (char)r.ReadByte();
                        message.Parts.Add(new SharedPart(id, r.ReadString()));
                    }
                    long end = r.BaseStream.Position;
                    if (end + 4 != body.Length || r.ReadUInt32() != Crc(body, 0, (int)end))
                    {
                        return SharedRead.Damaged;
                    }
                    m = message;
                    return string.Equals(message.Version, mine, StringComparison.Ordinal) ? SharedRead.Ok : SharedRead.OtherVersion;
                }
            }
            catch (Exception)
            {
                m = null;
                return SharedRead.Damaged;
            }
        }

        // The deflated body back, or null for anything that is not one of these messages or would unpack too large.
        private static byte[] Unpack(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 13 || bytes.Length > MostPacked)
            {
                return null;
            }
            for (int i = 0; i < 4; i++)
            {
                if (bytes[i] != Outer[i])
                {
                    return null;
                }
            }
            int length = BitConverter.ToInt32(bytes, 4);
            if (length <= 0 || length > MostUnpacked || BitConverter.ToInt32(bytes, 8) != bytes.Length - 12)
            {
                return null;   // the deflate stream itself would not notice bytes after its end
            }
            try
            {
                var body = new byte[length];
                using (var deflate = new DeflateStream(new MemoryStream(bytes, 12, bytes.Length - 12, false), CompressionMode.Decompress))
                {
                    int at = 0;
                    while (at < length)
                    {
                        int n = deflate.Read(body, at, length - at);
                        if (n <= 0)
                        {
                            return null;   // shorter than it says
                        }
                        at += n;
                    }
                    if (deflate.ReadByte() >= 0)
                    {
                        return null;       // longer than it says
                    }
                }
                return body;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>CRC-32 (the common polynomial, reflected) of a stretch of bytes.</summary>
        public static uint Crc(byte[] data, int from, int count)
        {
            uint c = 0xFFFFFFFFu;
            for (int i = from; i < from + count; i++)
            {
                c = Table[(c ^ data[i]) & 0xFF] ^ (c >> 8);
            }
            return c ^ 0xFFFFFFFFu;
        }

        private static uint[] MakeTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                }
                table[n] = c;
            }
            return table;
        }
    }
}
