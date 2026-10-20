using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// Your own AI's secrets on this PC: the API keys, and the ChatGPT sign-in's tokens. One file per Windows user, outside
    /// every save (the game's data folder, RLO Texts/service-keys.bin), sealed with Windows' own protection for the user's
    /// data (DPAPI: only this Windows user on this PC can open it; a copy of the file elsewhere opens nowhere), and written
    /// whole each time through a file of its own that then takes its place. Where Windows cannot seal it, nothing is kept,
    /// and the player is told. A key is never shown again once typed: <see cref="Has"/> says only whether there is one.
    /// The names are the service's ("key.4") or the sign-in's ("chatgpt.refresh"); nothing here is logged or saved in a
    /// game. Thread-safe (the game's thread, the runner's workers, the sign-in). Game-free (EconomyCheck, TextsServiceChecks.cs).
    /// COST: a file read when the service starts, a write when a key or a token changes.
    /// </summary>
    public sealed class TextKeys
    {
        public const string FileName = "service-keys.bin";
        private const int UiForbidden = 0x1;                 // CRYPTPROTECT_UI_FORBIDDEN: never a prompt
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RealLifeOverhaul.Texts.TextKeys");   // (this app's own: another app of the same user cannot open the file as its own)

        private readonly object _lock = new object();
        private readonly string _path;
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);
        private bool _read;

        public TextKeys(string folder)
        {
            _path = Path.Combine(folder, FileName);
        }

        /// <summary>Why the last read or write failed, for the log (no secret in it); null when it did not.</summary>
        public string Why { get; private set; }

        /// <summary>True when the file can be sealed here (Windows' protection answered once).</summary>
        public static bool Sealable()
        {
            try
            {
                byte[] sealedBytes = Seal(new byte[] { 1, 2, 3 });
                return sealedBytes != null && Open(sealedBytes) is byte[] back && back.Length == 3 && back[2] == 3;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public string Get(string name)
        {
            lock (_lock)
            {
                Load();
                return _values.TryGetValue(name, out string v) ? v : null;
            }
        }

        public bool Has(string name) => !string.IsNullOrEmpty(Get(name));

        /// <summary>Sets (or with null or empty takes out) each name and value pair, then writes the file once. False if it could not be kept.</summary>
        public bool Set(params string[] namesAndValues)
        {
            lock (_lock)
            {
                Load();
                for (int i = 0; i + 1 < namesAndValues.Length; i += 2)
                {
                    if (string.IsNullOrEmpty(namesAndValues[i + 1]))
                    {
                        _values.Remove(namesAndValues[i]);
                    }
                    else
                    {
                        _values[namesAndValues[i]] = namesAndValues[i + 1];
                    }
                }
                return Write();
            }
        }

        /// <summary>Every value kept, for taking them out of a line before it is logged.</summary>
        public string[] Secrets()
        {
            lock (_lock)
            {
                Load();
                var all = new string[_values.Count];
                _values.Values.CopyTo(all, 0);
                return all;
            }
        }

        private void Load()
        {
            if (_read)
            {
                return;
            }
            _read = true;
            _values.Clear();
            try
            {
                if (!File.Exists(_path))
                {
                    return;
                }
                byte[] plain = Open(File.ReadAllBytes(_path));
                if (plain == null)
                {
                    Why = "the keys' file could not be opened by this Windows user";
                    return;
                }
                Parse(Encoding.UTF8.GetString(plain), _values);
                Array.Clear(plain, 0, plain.Length);
            }
            catch (Exception e)
            {
                Why = "the keys' file could not be read (" + e.GetType().Name + ")";
            }
        }

        private bool Write()
        {
            try
            {
                byte[] plain = Encoding.UTF8.GetBytes(Text(_values));
                byte[] sealedBytes = Seal(plain);
                Array.Clear(plain, 0, plain.Length);
                if (sealedBytes == null)
                {
                    Why = "Windows could not protect the keys on this PC, so none is kept";
                    return false;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                string next = _path + ".new";
                File.WriteAllBytes(next, sealedBytes);
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
                File.Move(next, _path);
                Why = null;
                return true;
            }
            catch (Exception e)
            {
                Why = "the keys' file could not be written (" + e.GetType().Name + ")";
                return false;
            }
        }

        /// <summary>The values as the sealed file holds them: one "name=value" a line (values never hold a line break).</summary>
        public static string Text(Dictionary<string, string> values)
        {
            var t = new StringBuilder(256);
            foreach (KeyValuePair<string, string> kv in values)
            {
                t.Append(kv.Key).Append('=').Append(kv.Value.Replace("\r", string.Empty).Replace("\n", string.Empty)).Append('\n');
            }
            return t.ToString();
        }

        /// <summary>The values of such a text, into <paramref name="into"/>.</summary>
        public static void Parse(string text, Dictionary<string, string> into)
        {
            foreach (string line in text.Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq > 0)
                {
                    into[line.Substring(0, eq)] = line.Substring(eq + 1);
                }
            }
        }

        // ---------------------------------------------------------------- Windows' protection of a user's data (crypt32)

        [StructLayout(LayoutKind.Sequential)]
        private struct Blob
        {
            public int Size;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref Blob dataIn, string description, ref Blob entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref Blob dataIn, IntPtr description, ref Blob entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        private static byte[] Seal(byte[] plain) => Crypt(plain, true);

        private static byte[] Open(byte[] sealedBytes) => Crypt(sealedBytes, false);

        private static byte[] Crypt(byte[] data, bool seal)
        {
            GCHandle dataPin = GCHandle.Alloc(data, GCHandleType.Pinned);
            GCHandle entropyPin = GCHandle.Alloc(Entropy, GCHandleType.Pinned);
            try
            {
                var input = new Blob { Size = data.Length, Data = dataPin.AddrOfPinnedObject() };
                var entropy = new Blob { Size = Entropy.Length, Data = entropyPin.AddrOfPinnedObject() };
                Blob output;
                bool done = seal
                    ? CryptProtectData(ref input, "RLO Texts", ref entropy, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output);
                if (!done || output.Data == IntPtr.Zero)
                {
                    return null;
                }
                try
                {
                    var result = new byte[output.Size];
                    Marshal.Copy(output.Data, result, 0, output.Size);
                    return result;
                }
                finally
                {
                    LocalFree(output.Data);
                }
            }
            catch (Exception)
            {
                return null;   // (no crypt32 here: nothing is kept)
            }
            finally
            {
                dataPin.Free();
                entropyPin.Free();
            }
        }
    }
}
