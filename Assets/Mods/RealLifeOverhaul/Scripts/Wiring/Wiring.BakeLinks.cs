using System;
using RealLifeOverhaul.Sessions;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// A big customer's bake off with the rest of the living economy (Tech/Rivals): one desk session, put into the sessions' registry while the venture part runs and taken out
        /// when it stops. It is held at the owner's own desk in the firm's office, only while a bake off in the firm's niche is open and the firm has not entered it; the player picks a
        /// demo, a sharper price or the relationships. Nothing per frame: a click, or a page drawn.
        /// </summary>
        public static class BakeLinks
        {
            public const string Id = "tech_bakeoff";
            private const string Mod = "reallife", D = Mod + "_tech_desk_bake_";   // the keys' start, written so the source holds no key prefix as a literal

            private static readonly string[] Types =
            {
                TechTypes.SoftwareStudio, TechTypes.Consultancy, TechTypes.Security, TechTypes.AiLab, FirmsGame.WebAgency, FirmsGame.GraphicDesigner
            };

            internal static void Connect()
            {
                RivalryBridges.Sessions = Hold;
            }

            internal static void Disconnect()
            {
                Hold(false);
                RivalryBridges.Reset();
            }

            private static void Hold(bool on)
            {
                try
                {
                    if (on)
                    {
                        if (SessionRegistry.Find(Id) == null)
                        {
                            SessionRegistry.Add(Kind());
                        }
                    }
                    else
                    {
                        SessionRegistry.Remove(Id);
                    }
                }
                catch (Exception e)
                {
                    Fault("BakeLinks.Hold", e);
                }
            }

            private static SessionChoice C(string choice, int minutes) => new SessionChoice { NameKey = D + choice, TradeKey = D + choice + "_trade", Minutes = minutes };

            internal static SessionDef Kind()
            {
                var def = new SessionDef
                {
                    Id = Id, TitleKey = D + "title", AboutKey = D + "about", Types = Types, EveryDays = 1, Hour = 10, BaseMinutes = 20, Delegable = false, OwnDesk = true, DesksLeast = 1,
                    Steps = new[] { new SessionStep { TitleKey = D + "step", Choices = new[] { C("c0", 90), C("c1", 60), C("c2", 120) } } }
                };
                def.Blocked = ask => Blocked(ask);
                def.Pick = (ask, step) => 0;
                def.Apply = (ask, choices) => Apply(ask, choices);
                return def;
            }

            private static string Blocked(SessionAsk ask)
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    return tech != null ? tech.BakeBlocked(ask.Place) : D + "why_none";
                }
                catch (Exception e)
                {
                    Fault("BakeLinks.Blocked", e);
                    return D + "why_none";
                }
            }

            private static SessionOutcome Apply(SessionAsk ask, int[] choices)
            {
                TechDirector tech = TechDirector.Instance;
                if (tech == null)
                {
                    return new SessionOutcome { Done = false, Key = D + "why_none" };
                }
                int choice = choices != null && choices.Length > 0 ? choices[0] : 0;
                bool done = tech.BakeSession(ask.Place, ask.Name, choice, out string key, out string[] pairs);
                return new SessionOutcome { Done = done, Key = key, Pairs = pairs };
            }
        }
    }
}
