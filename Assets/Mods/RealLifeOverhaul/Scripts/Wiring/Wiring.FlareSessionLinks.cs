using System;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Sessions;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Flare's desk session (the brief's eleventh step: "a social media session at an office desk (a content plan for the week, answering a crisis),
        /// delegable to a hired social media manager"): the week on Flare planned at the owner's desk in one of the player's offices, or by that office's
        /// manager, once a week whichever office holds it first; its choices are Flare's rules (FlareCityRules.Plan, FlareDirector.Desk). In the
        /// sessions' registry while Flare in the city runs (FlareBridges.Desk), taken out when it stops. Nothing per frame: a click or a manager's hour.
        /// </summary>
        public static class FlareSessionLinks
        {
            // The player's offices: the tech firms' and the game's two office businesses, as the weekly team meeting's.
            private static readonly string[] Offices =
            {
                TechTypes.SoftwareStudio, TechTypes.GameStudio, TechTypes.Consultancy, TechTypes.Security, TechTypes.AiLab, FirmsGame.WebAgency, FirmsGame.GraphicDesigner
            };

            private static SessionDef _kind;

            internal static void Connect()
            {
                _kind = Kind();
                FlareBridges.Desk = Hold;
            }

            internal static void Disconnect()
            {
                Hold(false);
                FlareBridges.Desk = null;
                _kind = null;
            }

            // Listed while Flare in the city runs, taken out when it stops: no desk offers a plan nobody can make.
            private static void Hold(bool on)
            {
                try
                {
                    if (on && _kind != null)
                    {
                        if (SessionRegistry.Find(FlareCityRules.SessionId) == null)
                        {
                            SessionRegistry.Add(_kind);
                        }
                    }
                    else
                    {
                        SessionRegistry.Remove(FlareCityRules.SessionId);
                    }
                }
                catch (Exception e)
                {
                    Fault("FlareSessionLinks.Hold", e);
                }
            }

            // a choice by its whole key (its trade-off is the key and "_trade")
            private static SessionChoice C(string key, int minutes) => new SessionChoice { NameKey = key, TradeKey = key + "_trade", Minutes = minutes };

            internal static SessionDef Kind()
            {
                return new SessionDef
                {
                    Id = FlareCityRules.SessionId,
                    TitleKey = "reallife_flare_session_title",
                    AboutKey = "reallife_flare_session_about",
                    Types = Offices,
                    EveryDays = FlareCityRules.PlanWeek,
                    Hour = FlareCityRules.PlanHour,
                    BaseMinutes = 20,
                    Delegable = true,
                    OwnDesk = true,
                    DesksLeast = 1,
                    Steps = new[]
                    {
                        new SessionStep
                        {
                            TitleKey = "reallife_flare_session_step_posts",
                            Choices = new[] { C("reallife_flare_session_daily", 40), C("reallife_flare_session_few", 25), C("reallife_flare_session_none", 0) }
                        },
                        new SessionStep
                        {
                            TitleKey = "reallife_flare_session_step_reviews", Choices = new[] { C("reallife_flare_session_answer", 20), C("reallife_flare_session_leave", 0) }
                        },
                        new SessionStep
                        {
                            TitleKey = "reallife_flare_session_step_storm", Choices = new[] { C("reallife_flare_session_honest", 10), C("reallife_flare_session_quiet", 0) }
                        }
                    },
                    Blocked = Blocked,
                    Pick = Pick,
                    Apply = Apply
                };
            }

            private static string Blocked(SessionAsk ask)
            {
                FlareDirector flare = FlareDirector.Instance;
                if (flare == null || !flare.CityOn)
                {
                    return "reallife_flare_session_off";
                }
                return flare.DeskPlanned(ask.Day) ? "reallife_flare_session_held" : null;
            }

            // The sound choice at each step, which a manager makes: a post a day, every poor review answered, a storm answered honestly.
            private static int Pick(SessionAsk ask, int step)
            {
                return step == 0 ? FlareCityRules.PlanDaily : step == 1 ? FlareCityRules.ReviewsAnswer : FlareCityRules.StormHonest;
            }

            private static SessionOutcome Apply(SessionAsk ask, int[] choices)
            {
                FlareDirector flare = FlareDirector.Instance;
                int posts = choices != null && choices.Length > 0 ? choices[0] : FlareCityRules.PlanDaily;
                bool reviews = choices == null || choices.Length < 2 || choices[1] == FlareCityRules.ReviewsAnswer;
                bool storm = choices == null || choices.Length < 3 || choices[2] == FlareCityRules.StormHonest;
                int strength = SessionRules.Strength(ask.ByManager, ask.Skill);
                if (flare == null || !flare.DeskPlan(ask.Day, posts, reviews, storm, strength, ask.ByManager, out int planned, out int answered, out bool now))
                {
                    return new SessionOutcome { Done = false, Key = "reallife_flare_session_off" };
                }
                string words = FlareText.T(now ? "reallife_flare_session_storm_now" : storm ? "reallife_flare_session_storm_ready" : "reallife_flare_session_storm_quiet");
                return new SessionOutcome
                {
                    Done = true,
                    Key = "reallife_flare_session_done",
                    Pairs = new[] { "posts", SessionText.Whole(planned), "answered", SessionText.Whole(answered), "storm", words },
                    A = planned,
                    B = answered,
                    Days = FlareCityRules.PlanWeek
                };
            }
        }
    }
}
