using System;
using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Fame
{
    /// <summary>The options page's test tools for Standing. Each says in a notice why not when it cannot run.</summary>
    public sealed partial class FameDirector
    {
        private int _testRequest;

        private bool TestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Running)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        // A fault in a test tool switches Standing off like any other and never reaches the options page.
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
                Logger?.Info("Standing: test, somebody in the street knew the player (" + reaction + ", overall " + _file.Overall
                             + (d != null ? ", the district's opinion " + d.Opinion : string.Empty) + ").");
            });
        }

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

        public void TestBack()
        {
            if (!TestReady("Standing start again"))
            {
                return;
            }
            Guarded(() =>
            {
                Save.modData.Remove(FameStore.FileKey);
                Save.modData.Remove(FameStore.PressKey);
                Forget();
                Tell(NotificationType.Info, "reallife_fame_test_back_done", null);
            });
        }

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
                MakeParts();
                if (_reporter.Active || !_reporter.Begin(false))
                {
                    TestRefusals.Tell(Logger, "Standing reporter", TestRefusal.NoWayIn);
                    return;
                }
                Logger?.Info("Standing: test, a reporter comes up to the player.");
            });
        }

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
            Guarded(() => StartTestScene(FameInviteKind.Show));
        }

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
                        _file.Requests[i].State = FameRequestState.Lapsed;   // the test's request takes its place
                    }
                }
                var kind = (FameRequestKind)(_testRequest++ % ((int)FameRequestKind.Partnership + (place != null ? 1 : 0)));
                Requested(FameCity.Offer(_file, kind, Save.Day, Random.Range(0, 1000), FameGame.BusiestDistrict(), place));
            });
        }

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
            Guarded(() => StartTestScene(_testRequest++ % 2 == 0 ? FameInviteKind.Investors : FameInviteKind.CityEvent));
        }

        private void StartTestScene(FameInviteKind kind)
        {
            MakeParts();
            FameInvite invite = FamePress.Offer(_file, kind, Save.Day - FamePress.InviteAhead, Save.Hour);
            invite.State = FameInviteState.Accepted;
            StartScene(invite);
        }

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
                PressDay(Save.Day);
                Tell(NotificationType.Info, "reallife_fame_test_promises_done", _openPage, "count", FameText.N(Math.Max(1, judged)));
            });
        }
    }
}
