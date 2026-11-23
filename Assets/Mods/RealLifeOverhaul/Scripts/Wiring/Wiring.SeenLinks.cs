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
                RivalBridges.HeadLine = tycoon => SeenDirector.Instance != null ? SeenDirector.Instance.TycoonHead(tycoon) : string.Empty;
                RivalBridges.ShowHead = tycoon => SeenDirector.Instance != null && SeenDirector.Instance.ShowTycoonHead(tycoon);
                Economy.EstateBridges.HeadOffices = HeadPlaces;
                SeenBridges.Offer = OfferOf;
                SeenBridges.ShowOffer = id => Economy.ServicesDirector.Instance?.ShowRival(id);
                SeenBridges.Fund = tycoon => Schemes.SchemeDirector.Instance != null && Schemes.SchemeDirector.Instance.FundFrom(tycoon);
                SeenBridges.ShowFund = () => Schemes.SchemeDirector.Instance?.ShowFund();
                SeenBridges.Move = MoveOf;
                _paperBefore = News.Extra;
                News.Extra = Paper;
            }

            internal static void Disconnect()
            {
                SeenBridges.Reset();
                RivalBridges.SeenLine = null;
                RivalBridges.HeadLine = null;
                RivalBridges.ShowHead = null;
                Economy.EstateBridges.HeadOffices = null;
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            // The rivals' head offices as the property business writes a place, for the rivals' holdings to leave alone.
            private static void HeadPlaces(HashSet<string> into)
            {
                try
                {
                    SeenDirector seen = SeenDirector.Instance;
                    if (seen == null)
                    {
                        return;
                    }
                    var keys = new List<string>(32);
                    seen.HeadBuildings(keys);
                    for (int i = 0; i < keys.Count; i++)
                    {
                        if (SeenRules.Address(keys[i], out int cut, out int number) && cut > 0)
                        {
                            into.Add(Economy.EstateGame.Place(keys[i].Substring(0, cut), number));
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("SeenLinks.HeadPlaces", e);
                }
            }

            // What a rival has on the market with the player, from Services: a business they put up, or one they would take if the player does not.
            private static SeenOffer OfferOf(string rival)
            {
                try
                {
                    Economy.RivalSale v = Economy.ServicesDirector.Instance?.SaleOf(rival);
                    if (v == null)
                    {
                        return null;
                    }
                    bool selling = string.Equals(v.Seller, rival, StringComparison.Ordinal);
                    return new SeenOffer
                    {
                        Id = v.Id, Business = v.Business, Other = selling ? v.BuyerName : v.SellerName, Address = Economy.ServicesGame.Where(v.Place), Price = v.Price, Deadline = v.Deadline,
                        Selling = selling
                    };
                }
                catch (Exception e)
                {
                    Fault("SeenLinks.Offer", e);
                    return null;
                }
            }

            // What a tycoon has announced against the player, in the words of the rivals' own page; empty for nothing.
            private static string MoveOf(int tycoon)
            {
                try
                {
                    RivalFile f = Rivals();
                    RivalMove m = f != null ? f.Pending() : null;
                    return m != null && m.Tycoon == tycoon ? RivalText.Fill("reallife_rival_pending_line", "rival", f.Tycoons[tycoon].Def.Name, "what", RivalText.What(m), "day", m.Land.ToString())
                        : string.Empty;
                }
                catch (Exception e)
                {
                    Fault("SeenLinks.Move", e);
                    return string.Empty;
                }
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
