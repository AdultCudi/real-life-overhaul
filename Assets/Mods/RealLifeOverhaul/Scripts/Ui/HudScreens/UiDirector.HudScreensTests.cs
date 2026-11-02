using System;
using System.Globalization;
using Extensions;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The live HUD's tests (Options, Tests): a job and a shift at the top right, the objectives with every kind of group, and
    /// the day's summary at the top, each a sample laid over the live HUD until the test is pressed again (the game would
    /// otherwise make the player wait for a job, a mission or the day's end); and the game's own top bar, panels and
    /// objectives back for the session, to compare. The samples' words are the game's and the mod's own texts with the
    /// names of a sample city; nothing they show is pressed through to the game.
    /// </summary>
    public sealed partial class UiDirector
    {
        /// <summary>Options, Tests: a sample shift, job and building at the top right, or the player's own again.</summary>
        public void TestHudSides()
        {
            try
            {
                if (LiveHud() == null)
                {
                    return;
                }
                HudLive.SampleSides = HudLive.SampleSides == null ? SampleSides() : null;
                SampleSaid(HudLive.SampleSides != null);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the objectives with every kind of group, or the player's own again.</summary>
        public void TestHudGoals()
        {
            try
            {
                if (LiveHud() == null)
                {
                    return;
                }
                HudLive.SampleGoals = HudLive.SampleGoals == null ? SampleGoals() : null;
                SampleSaid(HudLive.SampleGoals != null);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the day's summary card at the top, or away again.</summary>
        public void TestHudSummary()
        {
            try
            {
                if (LiveHud() == null)
                {
                    return;
                }
                HudLive.SampleNote = HudLive.SampleNote == null ? SampleSummary() : null;
                SampleSaid(HudLive.SampleNote != null);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the game's own top bar, panels and objectives for the session, or the RLO HUD back.</summary>
        public void TestHudOwn()
        {
            try
            {
                if (TestHost() == null)
                {
                    return;
                }
                HudLive.GameOwn = !HudLive.GameOwn;
                Notifications.Show(NotificationType.Info, HudLive.GameOwn ? "reallife_ui_test_hud_own_shown" : "reallife_ui_test_hud_own_gone", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The HUD host while the live HUD stands over the city, or null with a notice saying why it does not yet.
        private static HudHost LiveHud()
        {
            HudHost host = TestHost();
            if (host != null && !HudLive.Live)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_hud_not_live", null, 8f);
                return null;
            }
            return host;
        }

        private static void SampleSaid(bool shown)
        {
            Notifications.Show(NotificationType.Info, shown ? "reallife_ui_test_hud_sample_shown" : "reallife_ui_test_hud_sample_gone", null, 8f);
            if (HudHost.Instance != null)
            {
                HudHost.Instance.Screen.Dirty = true;
            }
        }

        // ------------------------------------------------------------------------------------------------- the samples

        private static UiSidePanelModel[] SampleSides()
        {
            string employer = "Pioneer Game Studio";
            var shift = new UiSidePanelModel
            {
                Title = UiText.T("reallife_ui_hud_shift"), Meta = employer, Icon = UiIcon.Briefcase,
                Rows = new[]
                {
                    new UiInfoRow { Label = UiText.T("reallife_job_card_time").Replace("{on}", "2 h 45 min").Replace("{left}", "5 h 15 min") },
                    new UiInfoRow { Label = UiText.T("reallife_job_card_earned").Replace("{earned}", 105f.ToShortCurrencyFormat()), Ink = UiRole.Ok },
                },
                HasProgress = true, Progress = new UiProgressModel { Share = 0.34f },
            };
            var job = new UiSidePanelModel
            {
                Title = UiText.T("reallife_ui_hud_job"), Meta = employer, Icon = UiIcon.Briefcase, Collapsed = true,
                HeadName = employer, HeadSub = "4 7th Avenue",
                Rows = new[]
                {
                    new UiInfoRow { Label = UiText.T("common_today"), Value = Hours(6, 14) },
                    new UiInfoRow { Label = UiText.T("common_tomorrow"), Value = Hours(6, 14) },
                },
                Actions = new[]
                {
                    new UiActionModel { Label = UiText.T("common_setdestination"), Kind = UiButtonKind.Secondary, Icon = UiIcon.MapPin, Key = UiAction.Count },
                    new UiActionModel { Label = UiText.T("playerhud_currentjob_quit_job"), Kind = UiButtonKind.Destructive, Key = UiAction.Count },
                },
            };
            var building = new UiSidePanelModel
            {
                Title = "7 5th Avenue", Meta = UiText.T("reallife_ui_hud_your_business"), Icon = UiIcon.Store,
                HeadName = "Bloom & Co", HeadSub = UiText.T("ba:businesstype_florist") + ", Midtown", HeadChip = UiText.T("common_open"), HeadTone = UiRole.Ok,
                Rows = new[] { new UiInfoRow { Label = UiText.T("bizman_schedule_opening_hours"), Value = Hours(8, 20) } },
                SwitchLabel = UiText.T("bizman_temporarily_closed"),
                Actions = new[]
                {
                    new UiActionModel { Label = UiText.T("open_in_bizman"), Kind = UiButtonKind.Primary, Key = UiAction.Count },
                    new UiActionModel { Label = UiText.T("interior_designer"), Kind = UiButtonKind.Secondary, Key = UiAction.Count },
                },
                Tool = UiIcon.Grid,
            };
            return new[] { shift, job, building };
        }

        private static UiObjectivesModel SampleGoals()
        {
            string Plain(string words) => words.Replace("<u>", string.Empty).Replace("</u>", string.Empty);
            return new UiObjectivesModel
            {
                Title = UiText.T("tutorial_objectives"), LocateLabel = UiText.T("reallife_ui_hud_locate"),
                Meta = UiText.T("reallife_ui_hud_goals_open").Replace("{count}", "7"),
                Groups = new[]
                {
                    new UiObjectiveGroup
                    {
                        Title = UiText.T("bizman_alerts"), Icon = UiIcon.ListChecks,
                        Lines = new[]
                        {
                            new UiObjectiveLine { Text = Plain(UiText.T("tutorial_todotask_dirtyfloors").Replace("{businessname}", "Bloom & Co")), Locate = true, Urgent = true },
                            new UiObjectiveLine { Text = Plain(UiText.T("tutorial_todotask_paytaxes").Replace("{remainingDays}", "3")) },
                        },
                    },
                    new UiObjectiveGroup
                    {
                        Title = UiText.T("uncle_fred"), Icon = UiIcon.Person,
                        Lines = new[]
                        {
                            new UiObjectiveLine
                            {
                                Text = Plain(UiText.T("tutorial_quest_rent_your_apartment_objective_1").Replace("{targetAddress}", "12 Hudson Street")),
                                Locate = true,
                            },
                        },
                    },
                    new UiObjectiveGroup
                    {
                        Title = UiText.T("ba:skill_deliverydriver"), Icon = UiIcon.Truck,
                        Lines = new[]
                        {
                            new UiObjectiveLine { Text = "1 h 40 min", Close = UiText.T("common_cancel"), HasShare = true, Share = 0.45f },
                            new UiObjectiveLine { Text = "88 Elm Street (1.2 km)", Sub = "24 x Lilies", Locate = true },
                        },
                    },
                    new UiObjectiveGroup
                    {
                        Title = UiText.T("workout_plan"), Icon = UiIcon.Heartbeat,
                        Lines = new[] { new UiObjectiveLine { Text = UiText.T("workout_plan_objective").Replace("{workoutType}", "Cardio").Replace("{duration}", "30"), Done = true } },
                    },
                    new UiObjectiveGroup
                    {
                        Title = UiText.T("reallife_bld_track_header"), Icon = UiIcon.Hammer,
                        Lines = new[] { new UiObjectiveLine { Text = "Annex, 38th Street", Locate = true } },
                    },
                },
            };
        }

        private static UiHudNoticeModel SampleSummary()
        {
            int day = SaveGameManager.Current != null ? Math.Max(1, SaveGameManager.Current.Day - 1) : 1;
            return new UiHudNoticeModel
            {
                Kicker = UiText.T("daily_summary").Replace("{day}", day.ToString(CultureInfo.InvariantCulture)), Icon = UiIcon.Chart,
                BigLabel = UiText.T("total_profit"), Big = 2115f.ToShortCurrencyFormat(), BigTone = UiRole.OkText,
                Rows = new[] { new UiInfoRow { Label = UiText.T("daily_summary_businesses"), Value = "3" } }, RowCount = 1,
                Link = UiText.T("daily_summary_details"), LinkIcon = UiIcon.ChartLineUp,
            };
        }

        private static string Hours(int from, int to) =>
            UiText.T("contacts_today_opening_hours_open").Replace("{startingHour}", from.GetFormattedTime()).Replace("{endingHour}", to.GetFormattedTime());
    }
}
