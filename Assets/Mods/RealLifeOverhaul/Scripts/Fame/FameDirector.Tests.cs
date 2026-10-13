using System;
using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Fame
{
    /// <summary>
    /// The test tools of the options page: the daily round now, somebody in the street who knows the player now, the papers
    /// at their most so that the player is well known, and the file started again (more in FameDirector.Press.cs and
    /// FameDirector.City.cs). Each says in a notice why not when it cannot run.
    /// </summary>
    public sealed partial class FameDirector
    {
        private bool TestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Running || _file == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        // A test tool runs here: a fault in one switches Standing off for the session like any other, and never reaches the options page.
        private void Guarded(Action test)
        {
            try
            {
                test();
            }
            catch (Exception e)
            {
                Fault("a test tool", e);
            }
        }

        /// <summary>Test: Standing's daily round now, as at the start of a new day.</summary>
        public void TestDay()
        {
            if (!TestReady("Standing day"))
            {
                return;
            }
            Guarded(() =>
            {
                _file.SettledDay = Save.Day - 1;
                PlayDay(Save.Day);
                Tell(NotificationType.Info, "reallife_fame_test_day_done", _openPage, "recognition", FameText.N(_file.Recognition), "reputation", FameText.N(_file.Overall));
            });
        }

        /// <summary>Test: the nearest passer-by knows the player now, whatever the odds and the day's count.</summary>
        public void TestStreet()
        {
            if (!TestReady("Standing street"))
            {
                return;
            }
            if (BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle)
            {
                TestRefusals.Tell(Logger, "Standing street", TestRefusal.OnFootOnly);
                return;
            }
            Guarded(() =>
            {
                ThirdPersonCharacter who = FameGame.PasserBy(20f);
                if (who == null)
                {
                    TestRefusals.Tell(Logger, "Standing street", TestRefusal.NobodyNear, 20);
                    return;
                }
                FameDistrict d = _file.District(FameGame.DistrictHere());
                FameReaction reaction = FameRules.React(_file.Overall, _file.Wealth, d, Random.Range(0, 1000));
                FameGame.Face(who, Emoji(reaction));
                string line = FameRules.Speaks(reaction) ? FameText.StreetLine(reaction, _file.Wealth >= 50, Random.Range(0, 1000)) : "reallife_fame_test_street_look";
                Notifications.Show(NotificationType.Info, line, null, 7f);
                Logger?.Info("Standing: test, somebody in the street knew the player (" + reaction + ", overall " + _file.Overall + (d != null ? ", the district's opinion " + d.Opinion : string.Empty) + ").");
            });
        }

        /// <summary>Test: the papers at their most, and the round played again: the player is well known for a while.</summary>
        public void TestKnown()
        {
            if (!TestReady("Standing known"))
            {
                return;
            }
            Guarded(() =>
            {
                FameRules.Publicity(_file, FameRules.PressMost, Save.Day, FameCause.Press);
                _file.SettledDay = Save.Day - 1;
                PlayDay(Save.Day);
                Tell(NotificationType.Info, "reallife_fame_test_known_done", _openPage, "recognition", FameText.N(_file.Recognition), "tier", FameText.Tier(_file.Tier));
            });
        }

        /// <summary>Test: everything of Standing's for this save forgotten; a new file opens in a moment.</summary>
        public void TestBack()
        {
            if (!TestReady("Standing start again"))
            {
                return;
            }
            Guarded(() =>
            {
                Save.modData.Remove(FameStore.FileKey);
                TestBackMore();
                Forget();
                Tell(NotificationType.Info, "reallife_fame_test_back_done", null);
            });
        }

        partial void TestBackMore();

        /// <summary>Test: a reporter of the City Ledger comes up to the player now, whatever the odds.</summary>
        public void TestReporter()
        {
            if (!TestReady("Standing reporter"))
            {
                return;
            }
            if (BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle)
            {
                TestRefusals.Tell(Logger, "Standing reporter", TestRefusal.OnFootOnly);
                return;
            }
            Guarded(() =>
            {
                Parts();
                if (_reporter == null || _reporter.Active || !_reporter.Begin(false))
                {
                    TestRefusals.Tell(Logger, "Standing reporter", TestRefusal.NoWayIn);
                    return;
                }
                Logger?.Info("Standing: test, a reporter comes up to the player.");
            });
        }

        /// <summary>Test: the night show now, as if the player had accepted an invitation for this hour.</summary>
        public void TestShow()
        {
            if (!TestReady("Standing night show"))
            {
                return;
            }
            if (!Free())
            {
                TestRefusals.Tell(Logger, "Standing night show", TestRefusal.ScreenBusy);
                return;
            }
            Guarded(() =>
            {
                Parts();
                FameInvite invite = FamePress.Offer(_file, FameInviteKind.Show, Save.Day - FamePress.InviteAhead, Save.Hour);
                invite.State = FameInviteState.Accepted;
                StartScene(invite);
            });
        }

        /// <summary>Test: the city asks something of the player now: the next kind of request each time.</summary>
        public void TestRequest()
        {
            if (!TestReady("Standing request"))
            {
                return;
            }
            Guarded(() =>
            {
                List<BuildingRegistration> shops = FameGame.Businesses();
                string place = shops.Count > 0 ? FameGame.Place(shops[0]) : null;
                for (int i = 0; i < _file.Requests.Count; i++)
                {
                    if (_file.Requests[i].State == FameRequestState.Open)
                    {
                        _file.Requests[i].State = FameRequestState.Lapsed;   // (a test's request takes the place of one open)
                    }
                }
                FameRequestKind kind = (FameRequestKind)(_testRequest++ % ((int)FameRequestKind.Partnership + (place != null ? 1 : 0)));
                Requested(FameCity.Offer(_file, kind, Save.Day, Random.Range(0, 1000), FameGame.BusiestDistrict(), place));
            });
        }

        private int _testRequest;

        /// <summary>Test: an evening with investors now (every other time, a reception at City Hall), as if accepted for this hour.</summary>
        public void TestEvening()
        {
            if (!TestReady("Standing evening"))
            {
                return;
            }
            if (!Free())
            {
                TestRefusals.Tell(Logger, "Standing evening", TestRefusal.ScreenBusy);
                return;
            }
            Guarded(() =>
            {
                Parts();
                FameInvite invite = FamePress.Offer(_file, _testRequest++ % 2 == 0 ? FameInviteKind.Investors : FameInviteKind.CityEvent, Save.Day - FamePress.InviteAhead, Save.Hour);
                invite.State = FameInviteState.Accepted;
                StartScene(invite);
            });
        }

        /// <summary>Test: a bribe comes out now (one is paid first if there is none), with its case and its scandal.</summary>
        public void TestBribe()
        {
            if (!TestReady("Standing bribe"))
            {
                return;
            }
            Guarded(() =>
            {
                FameBribe open = null;
                for (int i = 0; i < _file.Bribes.Count; i++)
                {
                    open = _file.Bribes[i].Surfaced < 0 ? _file.Bribes[i] : open;
                }
                if (open == null)
                {
                    open = FameCity.Bribed(_file, "test", 500, Save.Day);
                }
                open.Day = Save.Day - FameCity.SurfaceAfterDays;
                FameBribe b = FameCity.Surface(_file, Save.Day, -1);
                if (b != null)
                {
                    Surfaced(b, Save.Day);
                }
            });
        }

        /// <summary>Test: a push on the council decided now (one is made for the first change on offer if none is pending).</summary>
        public void TestCouncil()
        {
            if (!TestReady("Standing council"))
            {
                return;
            }
            Guarded(() =>
            {
                FameLobby l = FameCity.Pending(_file);
                if (l == null)
                {
                    var levers = new List<FameLever>(16);
                    FameBridges.Levers?.Invoke(levers);
                    if (levers.Count == 0)
                    {
                        TestRefusals.Tell(Logger, "Standing council", TestRefusal.FeatureOff);
                        return;
                    }
                    l = FameCity.Lobby(_file, levers[0].Lever, levers[0].Position, Save.Day, Random.Range(0, 1000));
                }
                l.DecideDay = Save.Day;
                FameStory story = FameCity.Decide(_file, l, Save.Day, Random.Range(0, 100));
                Decided(l, story, Save.Day);
            });
        }

        /// <summary>Test: every open promise judged now, as if its day had come.</summary>
        public void TestPromises()
        {
            if (!TestReady("Standing promises"))
            {
                return;
            }
            Guarded(() =>
            {
                int judged = 0;
                for (int i = 0; i < _file.Promises.Count; i++)
                {
                    if (_file.Promises[i].State == FamePromiseState.Open)
                    {
                        _file.Promises[i].Due = Save.Day;
                        judged++;
                    }
                }
                if (judged == 0)
                {
                    FamePress.Promise(_file, FamePress.PromiseFor(_file), FameCause.Interview, Save.Day - FamePress.PromiseDays);
                }
                _file.NoticeDay = -1;
                _file.StoryDay = -1;
                DayMore(Save.Day);
                Tell(NotificationType.Info, "reallife_fame_test_promises_done", _openPage, "count", FameText.N(Math.Max(1, judged)));
            });
        }
    }
}
