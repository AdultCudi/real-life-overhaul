using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The tech sector and the professions with the rest of the mod (Tech/TechBridges). The tech sector calls no other
        /// feature and none calls it; what they have to do with each other is here.
        ///
        ///   Esteem     the player's standing as an employer, -10 to 10: Standing's workers (FamePress.Esteem), as the company reads it
        ///   Rival      a rival of the game's own with businesses to its name, to put a name to an approach (BusinessGame.AnyRival)
        ///   Held       the company's own rival offers and departures (CompanyState.Employee): nobody is approached twice at once
        ///   Standing   a lay-off, a rise refused or a promotion moves the workers' part of Standing by a point (FameDirector.Moved)
        ///   the paper  the tech sector's stories, chained on to the City Ledger's other pieces (News.Extra), the same for a
        ///              day however often it is asked
        ///
        /// Everything is read from what those features already keep in public; nothing of theirs is changed.
        /// </summary>
        public static class TechLinks
        {
            private static Action<int, List<NewsItem>> _paperBefore;
            private static readonly List<TechStory> Stories = new List<TechStory>(4);

            internal static void Connect()
            {
                TechBridges.Esteem = Esteem;
                TechBridges.Rival = Rival;
                TechBridges.Standing = Standing;
                TechBridges.Held = Held;
                _paperBefore = News.Extra;
                News.Extra = Paper;
            }

            internal static void Disconnect()
            {
                TechBridges.Reset();
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            private static int Esteem()
            {
                FameFile f = FameDirector.Instance != null ? FameDirector.Instance.File : null;
                return f != null ? FamePress.Esteem(f) : 0;
            }

            private static string Rival(BuildingRegistration reg)
            {
                try
                {
                    RivalData rival = reg != null ? BusinessGame.AnyRival(reg) : null;
                    return rival != null ? rival.rivalName : null;
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Rival", e);
                    return null;
                }
            }

            private static bool Held(string id)
            {
                try
                {
                    Economy.CompanyDirector company = Economy.CompanyDirector.Instance;
                    Economy.StaffFile f = company != null && company.Running && company.State != null ? company.State.Employee(id) : null;
                    return f != null && (f.PoachDay > 0 || f.LeaveDay > 0);
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Held", e);
                    return false;
                }
            }

            private static void Standing(int points)
            {
                try
                {
                    if (points != 0)
                    {
                        FameDirector.Instance?.Moved(FamePart.Workers, FameCause.Staff, points);
                    }
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Standing", e);
                }
            }

            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Paper (the pieces before)", e);
                }
                try
                {
                    TechDirector director = TechDirector.Instance;
                    if (director == null)
                    {
                        return;
                    }
                    Stories.Clear();
                    director.Stories(day, Stories);
                    for (int i = 0; i < Stories.Count; i++)
                    {
                        TechStory s = Stories[i];
                        var data = new Dictionary<string, string>(2);
                        if (s.AName.Length > 0)
                        {
                            data[s.AName] = s.A;
                        }
                        if (s.BName.Length > 0)
                        {
                            data[s.BName] = s.B;
                        }
                        into.Add(new NewsItem { Key = s.Key, Data = data });
                    }
                    Stories.Clear();
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Paper", e);
                }
            }
        }
    }
}
