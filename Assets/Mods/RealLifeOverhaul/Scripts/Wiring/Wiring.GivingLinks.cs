using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Giving;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Giving (Giving/GivingBridges) with the rest of the mod:
        ///
        ///   Reserved           the investors' floor of the player's firms (Tech's firm account): only cash over it is given
        ///   Income             the game's business sales, the income the year's deduction is capped on
        ///   WealthTier,
        ///   Recognition        how rich and how well known the player is, for how often a person asks
        ///   Integrity, Press   Standing: a gift's good name, and the papers
        ///   RivalWorth,
        ///   RivalPay,
        ///   RivalName          the rival tycoons give from their own worth to the same charities
        ///   GivingLine         the tycoons' lines on the rich list and their panel
        ///   News.Extra         the morning's City Ledger: the big gifts
        ///
        /// Asked on a day's pass or a press, never per frame; a fault is said once (<see cref="Fault"/>) and the bridge's default answered.
        /// </summary>
        public static class GivingLinks
        {
            private static Action<int, List<NewsItem>> _paperBefore;
            private static readonly List<GivingStory> Stories = new List<GivingStory>(4);

            internal static void Connect()
            {
                GivingBridges.Reserved = Reserved;
                GivingBridges.Income = (from, to) => CompanyGame.Sales(from, to);
                GivingBridges.WealthTier = WealthTier;
                GivingBridges.Recognition = () => Standing.Recognition;
                GivingBridges.Integrity = points => FameDirector.Instance != null ? FameDirector.Instance.Moved(FamePart.Integrity, FameCause.Charity, points) : 0;
                GivingBridges.Press = tenths => FameDirector.Instance != null ? FameDirector.Instance.Publicised(tenths, FameCause.Charity) : 0;
                GivingBridges.RivalWorth = RivalWorth;
                GivingBridges.RivalPay = RivalPay;
                GivingBridges.RivalName = RivalName;
                RivalBridges.GivingLine = tycoon => GivingDirector.Instance != null ? GivingDirector.Instance.RivalGivingLine(tycoon) : string.Empty;
                _paperBefore = News.Extra;
                News.Extra = Paper;
            }

            internal static void Disconnect()
            {
                GivingBridges.Reset();
                RivalBridges.GivingLine = null;
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            private static long Reserved()
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    return tech != null ? Math.Max(0L, tech.FloorNow / 100) : 0L;
                }
                catch (Exception e)
                {
                    Fault("GivingLinks.Reserved", e);
                    return 0L;
                }
            }

            private static int WealthTier()
            {
                try
                {
                    RivalFile f = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
                    return f != null ? Math.Max(0, Math.Min(5, f.Tier)) : 0;
                }
                catch (Exception e)
                {
                    Fault("GivingLinks.WealthTier", e);
                    return 0;
                }
            }

            // A tycoon's worth while they are forward and the rivals are not resting (a Going Public session); -1 otherwise.
            private static long RivalWorth(int index)
            {
                try
                {
                    RivalFile f = RivalDirector.Instance != null && !GoingPublic.InSession ? RivalDirector.Instance.File : null;
                    Tycoon t = f != null && index >= 0 && index < f.Tycoons.Length ? f.Tycoons[index] : null;
                    return t != null && t.Active ? t.Worth : -1L;
                }
                catch (Exception e)
                {
                    Fault("GivingLinks.RivalWorth", e);
                    return -1L;
                }
            }

            private static void RivalPay(int index, long amount)
            {
                try
                {
                    RivalFile f = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
                    Tycoon t = f != null && index >= 0 && index < f.Tycoons.Length ? f.Tycoons[index] : null;
                    if (t != null && t.Active)
                    {
                        t.Worth = Math.Max(0L, t.Worth - Math.Max(0L, amount));
                    }
                }
                catch (Exception e)
                {
                    Fault("GivingLinks.RivalPay", e);
                }
            }

            private static string RivalName(int index)
            {
                try
                {
                    return index >= 0 && index < RivalCatalogue.TycoonCount ? RivalCatalogue.Tycoons[index].Name : string.Empty;
                }
                catch (Exception e)
                {
                    Fault("GivingLinks.RivalName", e);
                    return string.Empty;
                }
            }

            // The gifts of yesterday in the morning's City Ledger: a very big one leads the front page, the rest follow the others' pieces.
            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("GivingLinks.Paper (the pieces before)", e);
                }
                try
                {
                    GivingDirector director = GivingDirector.Instance;
                    if (director == null)
                    {
                        return;
                    }
                    director.Stories(day, Stories);
                    bool led = false;
                    for (int i = 0; i < Stories.Count; i++)
                    {
                        var item = new NewsItem { Key = Stories[i].Key, Data = Stories[i].Data };
                        if (Stories[i].Lead && !led && into.Count > 0)
                        {
                            into.Insert(1, item);   // after the masthead
                            led = true;
                        }
                        else
                        {
                            into.Add(item);
                        }
                    }
                    Stories.Clear();
                }
                catch (Exception e)
                {
                    Fault("GivingLinks.Paper", e);
                }
            }
        }
    }
}
