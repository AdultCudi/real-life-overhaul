using System;
using System.IO;
using System.IO.Compression;
using BigAmbitions.SaveSystem.Legacy;
using OdinSerializer;
using Player.SaveSystem;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// The records to text and back with the game's own serializer, as SaveGameSerializationHelper writes and reads a
    /// whole save: Odin's binary format under the game's save policy (SaveGameSerializationPolicy: no obsolete member),
    /// read back through the game's own reader of older saves (IntToStringMigratingDataReader with LegacyHelper's
    /// migrations), so that a record kept across a game update reads as the save it came from does; then gzip and base64
    /// for the modData line. The line store could not hold them: a registration is some fifty fields deep (items, rota,
    /// designs, orders, staff), and any field a game update adds would be lost. Measured under the game's own Mono with
    /// the game's assemblies on a real save: the largest shop there (188 items, 306 interior designs) is 299 KB of Odin,
    /// 37 KB gzipped, 50,000 base64 characters, about 5 ms to write and 26 ms to read. A district of 537 businesses is
    /// 3.6 MB of Odin: one zlib stream took 42 ms of the save's frame, so a save compresses it in pieces on its workers
    /// while Odin writes (DistrictSaveZip), into the same gzip that the same reader reads. COST: at a save and a load only.
    /// </summary>
    internal static class DistrictSaveCodec
    {
        /// <summary>
        /// The store's whole line for the records: <paramref name="head"/> (the store's records, DistrictSaveStore.Head),
        /// then the payload, compressed in pieces by <paramref name="zip"/> as Odin writes; null when a piece failed its
        /// read back (zip.Fault), and the caller writes the payload with <see cref="Encode"/>.
        /// </summary>
        public static string Line(object[] records, string head, DistrictSaveZip zip)
        {
            zip.Begin();
            try
            {
                Write(records, zip);
            }
            catch
            {
                zip.Finish(string.Empty);   // every worker done before the fault goes up
                throw;
            }
            return zip.Finish(head);
        }

        /// <summary>
        /// The records as the store's payload text, through one stream of the runtime's own gzip: the way every payload was
        /// written before the pieces, kept for a save whose pieces failed.
        /// </summary>
        public static string Encode(object[] records)
        {
            using (var raw = new MemoryStream())
            {
                using (var zip = new GZipStream(raw, CompressionLevel.Optimal, true))
                {
                    Write(records, zip);
                }
                return Convert.ToBase64String(raw.GetBuffer(), 0, (int)raw.Length);
            }
        }

        /// <summary>The records back from the store's payload text; throws when it cannot be read.</summary>
        public static object[] Decode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new object[0];
            }
            byte[] packed = Convert.FromBase64String(text);
            using (var plain = new MemoryStream(packed.Length * 4))
            {
                using (var zip = new GZipStream(new MemoryStream(packed), CompressionMode.Decompress))
                {
                    zip.CopyTo(plain);
                }
                plain.Position = 0;
                return Read(plain);
            }
        }

        // As SaveGameSerializationHelper.SerializeBinaryData sets its context up.
        private static void Write(object[] records, Stream to)
        {
            var context = new SerializationContext();
            context.Config.SerializationPolicy = new SaveGameSerializationPolicy();
            context.Config.DebugContext.ErrorHandlingPolicy = ErrorHandlingPolicy.ThrowOnErrors;
            SerializationUtility.SerializeValue(records, to, DataFormat.Binary, context);
        }

        // As SaveGameSerializationHelper.DeserializeBinaryData reads a save.
        private static object[] Read(Stream from)
        {
            var context = new DeserializationContext();
            context.Config.AllowDeserializeInvalidData = true;
            context.Config.DebugContext.Logger = DefaultLoggers.UnityLogger;
            context.Config.DebugContext.LoggingPolicy = LoggingPolicy.LogWarningsAndErrors;
            using (var inner = new BinaryDataReader(from, context))
            using (var reader = new IntToStringMigratingDataReader(inner, LegacyHelper.GetMigrations(), LegacyHelper.GetLegacyEnumTypeNames(), LegacyHelper.GetLegacyTypeAliases()))
            {
                return SerializationUtility.DeserializeValue<object[]>(reader);
            }
        }
    }
}
