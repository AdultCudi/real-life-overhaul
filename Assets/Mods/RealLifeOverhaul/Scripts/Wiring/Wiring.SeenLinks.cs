using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.RivalsSeen;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The rivals seen around the city (RivalsSeen/SeenBridges) with the rest of the mod:
        ///
        ///   Rivals             the rivals' own feature: not started for this save yet, off, or running (SeenRules.RivalsState), so nothing is read before its
        ///                      tycoons can be
        ///   Tycoons            the rich list's tycoons who have come forward, each as the rivals' own file has them (Wiring.SeenTycoons, game-free)
        ///   TycoonHere         whether one of them is still there, asked as the hour turns
        ///   Playthrough        the playthrough's own id, made once with the save, for a new file's seed (as the stock market reads it)
        ///   Post               a society line on the City Ledger's Flare account
        ///   SeenLine           a tycoon's line on their panel and the rich list: their car and where they tend to be found
        ///   News.Extra         the morning's City Ledger: the society lines of the day before
        ///
        /// Asked on a look, a day's pass, an hour's or a press, never per frame; a fault is said once (<see cref="Fault"/>) and the bridge's default answered.
        /// </summary>
        public static class SeenLinks
        {
            private static Action<int, List<NewsItem>> _paperBefore;

            internal static void Connect()
            {
                SeenBridges.Rivals = RivalsNow;
                SeenBridges.Tycoons = Tycoons;
                SeenBridges.TycoonHere = TycoonHere;
                SeenBridges.Playthrough = Playthrough;
                SeenBridges.Post = (words, hour, minute) => FlareDirector.Instance != null && FlareDirector.Instance.Society(words, hour, minute);
                RivalBridges.SeenLine = tycoon => SeenDirector.Instance != null ? SeenDirector.Instance.TycoonLine(tycoon) : string.Empty;
                _paperBefore = News.Extra;
                News.Extra = Paper;
            }

            internal static void Disconnect()
            {
                SeenBridges.Reset();
                RivalBridges.SeenLine = null;
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            private static RivalFile Rivals()
            {
                RivalDirector rivals = RivalDirector.Instance;
                return rivals != null && rivals.Running && !rivals.Resting ? rivals.File : null;
            }

            // The rivals' own feature: off when it was never made, its option is off or a fault switched it off; running; or still to start for this save.
            private static int RivalsNow()
            {
                try
                {
                    RivalDirector rivals = RivalDirector.Instance;
                    return SeenRules.RivalsState(rivals != null, Settings.Rivals, rivals != null && rivals.Broken, rivals != null && rivals.Running, rivals != null && rivals.Resting);
                }
                catch (Exception e)
                {
                    Fault("SeenLinks.Rivals", e);
                    return SeenRules.RivalsOff;
                }
            }

            private static void Tycoons(List<SeenSubject> into)
            {
                try
                {
                    SeenTycoons.Map(Rivals(), RivalGame.Day, into);
                }
                catch (Exception e)
                {
                    Fault("SeenLinks.Tycoons", e);
                }
            }

            // With the rivals' file away (switched off this second) nobody is taken to be gone: the next look reads the day's rivals again without them.
            private static bool TycoonHere(int index)
            {
                try
                {
                    RivalFile f = Rivals();
                    return f == null || SeenTycoons.Here(f, index);
                }
                catch (Exception e)
                {
                    Fault("SeenLinks.TycoonHere", e);
                    return true;
                }
            }

            // The save's own seed, made by the game when the save was (RngHelper.GenerateGameSeed), else its character's id: the stock market's playthrough too.
            private static string Playthrough()
            {
                try
                {
                    GameInstance save = SaveGameManager.Current;
                    return save == null ? null : !string.IsNullOrEmpty(save.seed) ? save.seed : save.characterId ?? "city";
                }
                catch (Exception e)
                {
                    Fault("SeenLinks.Playthrough", e);
                    return null;
                }
            }

            // The society lines of yesterday in the morning's City Ledger, after the others' pieces.
            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("SeenLinks.Paper (the pieces before)", e);
                }
                try
                {
                    SeenDirector.Instance?.Stories(day, into);
                }
                catch (Exception e)
                {
                    Fault("SeenLinks.Paper", e);
                }
            }
        }
    }
}
