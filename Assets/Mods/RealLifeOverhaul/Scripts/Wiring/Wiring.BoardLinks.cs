using System;
using RealLifeOverhaul.Sessions;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The board's question to a founder whose firm account is short (the startup money rules, Tech/Venture/FirmAccount.cs): one desk
        /// session, put into the sessions' registry while the venture part runs and taken out when it stops. It is held at the owner's own
        /// desk in the firm's office, only while the board has a question open, and no manager takes it: repay what was taken, explain
        /// (the player's Accounting and Law count), or stonewall. Nothing per frame: a click, or a page drawn.
        /// </summary>
        public static class BoardLinks
        {
            public const string Id = "tech_board";
            private const string Mod = "reallife", D = Mod + "_tech_desk_board_";   // the keys' start, written so the source holds no key prefix as a literal

            private static readonly string[] Types =
            {
                TechTypes.SoftwareStudio, TechTypes.GameStudio, TechTypes.Consultancy, TechTypes.Security, TechTypes.AiLab, FirmsGame.WebAgency, FirmsGame.GraphicDesigner
            };

            internal static void Connect()
            {
                TechBridges.BoardSessions = Hold;
            }

            internal static void Disconnect()
            {
                Hold(false);
                TechBridges.BoardSessions = null;
            }

            private static void Hold(bool on)
            {
                try
                {
                    if (on)
                    {
                        SessionRegistry.Add(Kind());
                    }
                    else
                    {
                        SessionRegistry.Remove(Id);
                    }
                }
                catch (Exception e)
                {
                    Fault("BoardLinks.Hold", e);
                }
            }

            private static SessionChoice C(string choice, int minutes) => new SessionChoice { NameKey = D + choice, TradeKey = D + choice + "_trade", Minutes = minutes };

            internal static SessionDef Kind()
            {
                var def = new SessionDef
                {
                    Id = Id, TitleKey = D + "title", AboutKey = D + "about", Types = Types, EveryDays = 1, Hour = 10, BaseMinutes = 20, Delegable = false, OwnDesk = true, DesksLeast = 1,
                    Steps = new[] { new SessionStep { TitleKey = D + "step1", Choices = new[] { C("repay", 20), C("explain", 30), C("stonewall", 0) } } }
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
                    return tech != null ? tech.BoardBlocked(ask.Place) : D + "why_none";
                }
                catch (Exception e)
                {
                    Fault("BoardLinks.Blocked", e);
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
                bool done = tech.BoardSession(ask.Place, ask.Name, choice, out string key, out string[] pairs);
                return new SessionOutcome { Done = done, Key = key, Pairs = pairs };
            }
        }
    }
}
