using System;
using RealLifeOverhaul.Sessions;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The game studio's four desk sessions (the concept pitch, the milestone review, the playtest, the post-launch review),
        /// held at a game studio's desk or by its manager, put into the sessions' registry while the studio runs and taken out
        /// when it stops (TechBridges.StudioSessions). Their choices are the studio's rules (TechDirector.StudioDesk). The
        /// population's audience (StudioBridges.Population) is wired with the population (Wiring.PopulationLinks); Flare's
        /// bridges are left at their defaults here, nothing posted, until that workstream wires them.
        /// </summary>
        public static class StudioLinks
        {
            private static readonly SessionDef[] Kinds = new SessionDef[StudioRules.Sessions];

            internal static void Connect()
            {
                for (int k = 0; k < StudioRules.Sessions; k++)
                {
                    Kinds[k] = Kind(k);
                }
                TechBridges.StudioSessions = Hold;
                TechBridges.Desks = Desks;
            }

            internal static void Disconnect()
            {
                Hold(false);
                TechBridges.Desks = null;
                TechBridges.StudioSessions = null;
                StudioBridges.Reset();
            }

            // The kinds put in while the studio runs, taken out when it stops: a studio switched off shows no session of its own.
            private static void Hold(bool on)
            {
                try
                {
                    for (int k = 0; k < StudioRules.Sessions; k++)
                    {
                        if (on && Kinds[k] != null)
                        {
                            SessionRegistry.Add(Kinds[k]);
                        }
                        else
                        {
                            SessionRegistry.Remove(StudioRules.SessionIds[k]);
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("StudioLinks.Hold", e);
                }
            }

            // True while desk sessions run, so the studio's pitch is held at the desk; a fault reads as not running.
            private static bool Desks()
            {
                try
                {
                    return SessionDirector.On;
                }
                catch (Exception e)
                {
                    Fault("StudioLinks.Desks", e);
                    return false;
                }
            }

            private static readonly string[] GameStudio = { TechTypes.GameStudio };

            // a choice by its whole key (its trade-off is the key and "_trade"), and a step by its title's key
            private static SessionChoice C(string key, int minutes) => new SessionChoice { NameKey = key, TradeKey = key + "_trade", Minutes = minutes };

            private static SessionStep S(string titleKey, params SessionChoice[] choices) => new SessionStep { TitleKey = titleKey, Choices = choices };

            internal static SessionDef Kind(int kind)
            {
                var def = new SessionDef
                {
                    Id = StudioRules.SessionIds[kind],
                    Types = GameStudio,
                    EveryDays = kind == StudioRules.SessionPlaytest ? 10 : kind == StudioRules.SessionPostLaunch ? 7 : 1,   // a pitch and a milestone are due whenever one waits
                    Delegable = true,
                    OwnDesk = true,
                    DesksLeast = 2   // the owner's and one for a team
                };
                switch (kind)
                {
                    case StudioRules.SessionPitch:
                        def.TitleKey = "reallife_studio_pitch_title";
                        def.AboutKey = "reallife_studio_pitch_about";
                        def.Hour = 11;
                        def.BaseMinutes = 30;
                        def.Steps = new[]
                        {
                            S("reallife_studio_pitch_step_ambition", C("reallife_studio_pitch_lean", 0), C("reallife_studio_pitch_planned", 15), C("reallife_studio_pitch_bold", 30)),
                            S("reallife_studio_pitch_step_funding", C("reallife_studio_pitch_self", 0), C("reallife_studio_pitch_publisher", 15)),
                            S("reallife_studio_pitch_step_drm", C("reallife_studio_pitch_no_drm", 0), C("reallife_studio_pitch_drm", 0))
                        };
                        break;
                    case StudioRules.SessionMilestone:
                        def.TitleKey = "reallife_studio_milestone_title";
                        def.AboutKey = "reallife_studio_milestone_about";
                        def.Hour = 11;
                        def.BaseMinutes = 45;
                        def.Steps = new[]
                        {
                            S("reallife_studio_milestone_step_plan", C("reallife_studio_milestone_cut", 0), C("reallife_studio_milestone_hold", 0), C("reallife_studio_milestone_delay", 15)),
                            S("reallife_studio_milestone_step_pace", C("reallife_studio_milestone_crunch", 15), C("reallife_studio_milestone_normal", 0), C("reallife_studio_milestone_budget", 15))
                        };
                        break;
                    case StudioRules.SessionPlaytest:
                        def.TitleKey = "reallife_studio_playtest_title";
                        def.AboutKey = "reallife_studio_playtest_about";
                        def.Hour = 14;
                        def.BaseMinutes = 30;
                        def.Steps = new[]
                        {
                            S("reallife_studio_playtest_step_who", C("reallife_studio_playtest_team", 30), C("reallife_studio_playtest_closed", 60), C("reallife_studio_playtest_open", 90)),
                            S("reallife_studio_playtest_step_fixes", C("reallife_studio_playtest_fix_all", 0), C("reallife_studio_playtest_fix_worst", 0), C("reallife_studio_playtest_ship", 0))
                        };
                        break;
                    default:
                        def.TitleKey = "reallife_studio_postlaunch_title";
                        def.AboutKey = "reallife_studio_postlaunch_about";
                        def.Hour = 15;
                        def.BaseMinutes = 30;
                        def.Steps = new[]
                        {
                            S("reallife_studio_postlaunch_step_bugs", C("reallife_studio_postlaunch_hotfix", 30), C("reallife_studio_postlaunch_patch", 15), C("reallife_studio_postlaunch_leave", 0)),
                            S("reallife_studio_postlaunch_step_fans", C("reallife_studio_postlaunch_update", 15), C("reallife_studio_postlaunch_addon", 15), C("reallife_studio_postlaunch_listen", 0)),
                            S("reallife_studio_postlaunch_step_store", C("reallife_studio_postlaunch_hold", 0), C("reallife_studio_postlaunch_sale", 0), C("reallife_studio_postlaunch_drm_off", 0))
                        };
                        break;
                }
                def.Blocked = ask => Blocked(kind, ask);
                def.Pick = (ask, step) => Pick(kind, ask, step);
                def.Apply = (ask, choices) => Apply(kind, def, ask, choices);
                return def;
            }

            private static string Blocked(int kind, SessionAsk ask)
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    return tech != null ? tech.StudioBlocked(kind, ask.Place) : "reallife_studio_session_off";
                }
                catch (Exception e)
                {
                    Fault("StudioLinks.Blocked", e);
                    return "reallife_studio_session_off";
                }
            }

            private static int Pick(int kind, SessionAsk ask, int step)
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    return tech != null ? tech.StudioPick(kind, ask.Place, step) : 0;
                }
                catch (Exception e)
                {
                    Fault("StudioLinks.Pick", e);
                    return 0;
                }
            }

            // The studio applies the choices; the notice's words get the choices' names beside the game's figures.
            private static SessionOutcome Apply(int kind, SessionDef def, SessionAsk ask, int[] choices)
            {
                TechDirector tech = TechDirector.Instance;
                if (tech == null)
                {
                    return new SessionOutcome { Done = false, Key = "reallife_studio_session_off" };
                }
                int strength = SessionRules.Strength(ask.ByManager, ask.Skill);
                bool done = tech.StudioSession(kind, ask.Place, ask.Name, strength, choices, out string key, out string[] pairs);
                if (!done)
                {
                    return new SessionOutcome { Done = false, Key = key, Pairs = pairs };
                }
                var all = new string[pairs.Length + def.Steps.Length * 2];
                Array.Copy(pairs, all, pairs.Length);
                for (int i = 0; i < def.Steps.Length; i++)
                {
                    SessionChoice[] c = def.Steps[i].Choices;
                    int at = choices != null && i < choices.Length && choices[i] >= 0 && choices[i] < c.Length ? choices[i] : 0;
                    all[pairs.Length + i * 2] = "choice" + i;
                    all[pairs.Length + i * 2 + 1] = SessionText.T(c[at].NameKey);
                }
                return new SessionOutcome { Done = true, Key = key, Pairs = all };
            }
        }
    }
}
