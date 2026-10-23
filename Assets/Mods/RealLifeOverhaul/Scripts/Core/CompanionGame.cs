using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Meets the game for companion captures (contracts/companion/v1). A save carries the same report a live reader gets
    /// from <see cref="Companion.CaptureV1"/>; RLO never loads it back into play. Nothing per frame: a capture runs on a
    /// save or a reader's call, and costs one pass over the businesses, the employees and the credit accounts.
    /// </summary>
    internal static class CompanionGame
    {
        private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The report, or null outside a loaded session or in Going Public. Throws on a fault of the envelope.</summary>
        internal static bool Available(GameInstance game)
        {
            return game != null && ReferenceEquals(game, SaveGameManager.Current) && ModState.Loaded
                && Wiring.Connected && !GoingPublic.InSession;
        }

        public static string Capture(GameInstance game)
        {
            if (!Available(game))
            {
                return null;
            }
            return CompanionSnapshot.Capture(Meta.Changelog.Latest,
                CompanionSnapshot.Row("character", game.characterId, "day", game.Day, "hour", game.Hour, "minute", (int)game.Minute),
                CompanionBridges.Readers);
        }

        /// <summary>As <see cref="Capture"/>, but a fault gives null without diagnostics or latch changes: another mod's call never throws.</summary>
        public static string CaptureSafe(GameInstance game)
        {
            try
            {
                return Capture(game);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Called after the save handlers and SaveLines.PutBack. No report, or a failed one, removes an older report.</summary>
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

        /// <summary>The player's own established businesses. Type ids stay opaque; a reader declares its own model of them.</summary>
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

        /// <summary>Forgets which faults were logged, so a new session says each once again.</summary>
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
