using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Meets the game for companion captures. The ordinary save embeds the same report Link reads directly.
    /// Reports are never loaded into the simulation. A capture costs one pass over the requested reporting records.
    /// </summary>
    public static class CompanionGame
    {
        private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);

        public static string Capture(GameInstance game)
        {
            if (game == null || !ReferenceEquals(game, SaveGameManager.Current) || !ModState.Loaded
                || !Wiring.Connected || GoingPublic.InSession)
            {
                return null;
            }
            return CompanionSnapshot.Capture(Meta.Changelog.Latest,
                CompanionSnapshot.Row("character", game.characterId, "day", game.Day, "hour", game.Hour, "minute", (int)game.Minute),
                CompanionBridges.Readers, Failed);
        }

        /// <summary>Called after the normal save handlers. A failed or unsupported capture removes an older report.</summary>
        public static void Save(GameInstance game)
        {
            if (game == null)
            {
                return;
            }
            try
            {
                string report = Capture(game);
                if (report == null)
                {
                    game.modData?.Remove(CompanionSnapshot.Key);
                    return;
                }
                if (game.modData == null)
                {
                    game.modData = new Dictionary<string, string>();
                }
                game.modData[CompanionSnapshot.Key] = report;
            }
            catch (Exception e)
            {
                game.modData?.Remove(CompanionSnapshot.Key);
                Failed("capture", e);
            }
        }

        /// <summary>Owned business identities. Type ids stay opaque; a companion must declare a model separately.</summary>
        public static CompanionModule Businesses()
        {
            var rows = new List<object>();
            List<BuildingRegistration> all = SaveGameManager.Current.BuildingRegistrations;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasEstablishedBusiness || !reg.HasValidAddress)
                {
                    continue;
                }
                rows.Add(CompanionSnapshot.Row("street", reg.StreetName, "number", reg.StreetNumber,
                    "name", reg.BusinessName, "typeId", reg.businessTypeName,
                    "temporarilyClosed", reg.temporarilyClosed));
            }
            return new CompanionModule("ready", rows);
        }

        public static void Reset() => Reported.Clear();

        private static void Failed(string capability, Exception error)
        {
            if (Reported.Add(capability))
            {
                SaveHook.Logger?.Warn("Companion: " + capability + " could not be captured: " + error.Message);
            }
        }
    }
}
