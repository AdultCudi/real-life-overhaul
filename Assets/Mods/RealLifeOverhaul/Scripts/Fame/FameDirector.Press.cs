using System;
using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Fame
{
    /// <summary>
    /// The press (FamePress has the rules): reporters, the night show and a magazine, promises, the City Ledger's
    /// pieces, the customers standing brings, and help refused in front of others.
    /// </summary>
    public sealed partial class FameDirector
    {
        private const float NeedNear = 25f, NeedGone = 70f;   // metres: near enough to have seen it, far enough to have walked on
        private const float WitnessWithin = 30f;              // metres

        private readonly List<FameAsk> _asks = new List<FameAsk>(3);
        private readonly Dictionary<string, int> _footfall = new Dictionary<string, int>(16, StringComparer.Ordinal);   // as last written
        private FameReporter _reporter;
        private FamePanel _panel;

        // ---- the scene in the studio, or at an evening (FameDirector.City.cs)
        private FameInvite _scene;
        private FameVenue _venue;
        private int _sceneAt, _sceneGood, _sceneHard, _scenePress;

        private bool _sawNeed, _helped;
        private Vector3 _needAt;
        private int _needTick;

        /// <summary>Standing's part of a business's customers, per cent; 0 while Standing is off.</summary>
        public static int FootfallOf(BuildingRegistration reg)
        {
            FameDirector d = Instance;
            FameFile f = d != null ? d.File : null;
            if (f == null || reg == null)
            {
                return 0;
            }
            string district = GameRegistrations.District(reg);
            return FamePress.Footfall(f, f.District(district)) + FameCity.BoostAt(f, FameGame.Place(reg), district, FameGame.Day);
        }

        private void MakeParts()
        {
            if (_reporter == null)
            {
                _reporter = new FameReporter(this);
                _panel = new FamePanel();
            }
        }

        private void PressSecond(int day)
        {
            MakeParts();
            _reporter.Tick();
            _panel.Watch();
            if (_scene != null && !_panel.Active)
            {
                SceneOver(false);   // the window went with the game's interface
            }
            if (++_needTick >= 2)
            {
                _needTick = 0;
                WatchNeed(day);
            }
        }

        private void PressStandDown()
        {
            if (_reporter != null)
            {
                _reporter.Clear();
            }
            if (_panel != null && _panel.Active)
            {
                _panel.End();
            }
            _scene = null;
            _footfall.Clear();
            _sawNeed = false;
        }

        private void PressHour(int day, int hour)
        {
            if (!Settings.StandingPress)
            {
                return;
            }
            for (int i = 0; i < _file.Invites.Count; i++)
            {
                FameInvite invite = _file.Invites[i];
                FameInviteState before = invite.State;
                if (FamePress.Due(_file, invite, day, hour, Free()))
                {
                    StartScene(invite);
                }
                else if (invite.State == FameInviteState.Missed && before != FameInviteState.Missed)
                {
                    Logger?.Info("Standing: the " + invite.Kind + " of day " + invite.Day + " was missed: the papers say so (recognition " + _file.Recognition + ").");
                    if (FameRules.MayNotice(_file, day))
                    {
                        Tell(NotificationType.Warning, "reallife_fame_note_missed", _openPage, "what", FameText.InviteName(invite.Kind));
                    }
                }
            }
            if (_scene != null || _reporter.Active || !FameGame.OnFootOutside() || Wiring.Scenes.AnyLargeSceneActive)
            {
                return;
            }
            if (FamePress.ReporterMay(_file, day) && Random.Range(0, 1000) < FamePress.ReporterChance(_file.Recognition))
            {
                if (_reporter.Begin(false))
                {
                    _file.ReporterDay = day;
                    Logger?.Info("Standing: a reporter comes up to the player in the street (recognition " + _file.Recognition + ").");
                }
            }
            else if (FamePress.PhotoMay(_file, day) && Random.Range(0, 1000) < FamePress.PhotoChance(_file.Recognition))
            {
                if (_reporter.Begin(true))
                {
                    _file.PhotoDay = day;
                    Logger?.Info("Standing: a photographer comes up to the player in the street (recognition " + _file.Recognition + ").");
                }
            }
        }

        private void PressDay(int day)
        {
            CityDay(day);
            Footfall();
            if (!Settings.StandingPress)
            {
                return;
            }
            FameInvite invite = FamePress.InviteRoll(_file, day, Random.Range(0, 1000));
            if (invite != null)
            {
                Invited(invite);
            }
            JudgePromises(day);
            FameStory story = FamePress.Milestone(_file);
            if (story == FameStory.None)
            {
                string district;
                story = FamePress.Criticism(_file, day, out district);
                if (story != FameStory.None)
                {
                    FameRules.Publicity(_file, FamePress.StoryPress, day, FameCause.Reported);
                    Logger?.Info("Standing: the City Ledger writes about the player: " + story + (district != null ? " in " + district : string.Empty) + ".");
                }
            }
            Print(story, day);
        }

        private void JudgePromises(int day)
        {
            for (int i = 0; i < _file.Promises.Count; i++)
            {
                FamePromise p = _file.Promises[i];
                if (p.State != FamePromiseState.Open)
                {
                    continue;
                }
                FamePromiseState now = FamePress.Judge(_file, p, day, _file.CharityDay);
                if (now == FamePromiseState.Open)
                {
                    continue;
                }
                bool kept = now == FamePromiseState.Kept;
                Logger?.Info("Standing: the promise of day " + p.Made + " (" + p.Kind + ", goal " + p.Goal + ") is " + now + "; integrity " + _file.Rep[(int)FamePart.Integrity] + ".");
                Print(kept ? FameStory.PromiseKept : FameStory.PromiseBroken, day);
                if (FameRules.MayNotice(_file, day))
                {
                    Tell(kept ? NotificationType.Success : NotificationType.Warning, kept ? "reallife_fame_note_kept" : "reallife_fame_note_broken",
                        _openPage, "promise", FameText.PromiseWords(p.Kind));
                }
            }
        }

        /// <summary>A piece about the player for tomorrow's City Ledger, one a day at most; nothing while the press is switched off.</summary>
        internal void Print(FameStory story, int day)
        {
            if (story == FameStory.None || !Settings.StandingPress || !FamePress.StoryMay(_file, day))
            {
                return;
            }
            FameBridges.Story?.Invoke(FameText.StoryKey(story));
            Logger?.Info("Standing: a piece for the City Ledger (" + story + ").");
        }

        // After the daily round: each business whose customers Standing's part changed is written again.
        private void Footfall()
        {
            if (FameBridges.Footfall == null)
            {
                return;
            }
            List<BuildingRegistration> shops = FameGame.Businesses();
            for (int i = 0; i < shops.Count; i++)
            {
                string place = FameGame.Place(shops[i]);
                int now = FootfallOf(shops[i]), before;
                _footfall.TryGetValue(place, out before);
                if (now == before)
                {
                    continue;
                }
                _footfall[place] = now;
                try
                {
                    FameBridges.Footfall(shops[i], true);
                }
                catch (Exception e)
                {
                    Logger?.Warn("Standing: the customers of " + FameGame.Name(shops[i]) + " could not be written again (" + e.Message + ").");
                }
                Logger?.Info("Standing: " + FameGame.Name(shops[i]) + " " + (now > 0 ? "+" : string.Empty) + now + "% customers by the owner's standing (it was " + before + "%).");
            }
        }

        /// <summary>The questions of an interview at a venue; null while Standing is not running.</summary>
        internal List<FameAsk> Interview(FameVenue venue)
        {
            if (!Running)
            {
                return null;
            }
            var asks = new List<FameAsk>(3);
            FamePress.Questions(_file, venue, Random.Range(0, 1000), asks);
            return asks;
        }

        /// <summary>An answer given in an interview takes effect at once; it may make a promise.</summary>
        internal FameAnswer Answered(FameAsk ask, int option, FameVenue venue)
        {
            FameAnswer a = FamePress.Answer(ask, option, venue);
            if (!Running || Save == null)
            {
                return a;
            }
            int day = Save.Day;
            FameCause cause = venue == FameVenue.Show ? FameCause.Show : venue == FameVenue.Magazine ? FameCause.Magazine : FameCause.Interview;
            int press = FamePress.Apply(_file, a, cause, day);
            FamePromise promise = a.Promises ? FamePress.Promise(_file, ask.Promise, cause, day) : null;
            _scenePress += press;
            Logger?.Info("Standing: " + venue + ", " + ask.Question + (ask.Question == FameQuestion.Hard ? " about " + ask.Topic : string.Empty) + ", answer " + (option + 1)
                         + ": " + FameRules.Tenths(press) + " recognition from the papers" + (a.MoveA != 0 ? ", " + a.PartA + " " + a.MoveA : string.Empty)
                         + (a.MoveB != 0 ? ", " + a.PartB + " " + a.MoveB : string.Empty)
                         + (promise != null ? ", a promise (" + promise.Kind + ", goal " + promise.Goal + " by day " + promise.Due + ")"
                            : a.Promises ? ", no new promise (one of the kind or three are open)" : string.Empty) + ".");
            return a;
        }

        internal void Interviewed(FameVenue venue, int good, int hard)
        {
            if (!Running || Save == null)
            {
                return;
            }
            FameStory story = venue == FameVenue.Show ? (hard > good ? FameStory.ShowBad : FameStory.Show)
                : venue == FameVenue.Magazine ? FameStory.Magazine
                : hard > 0 ? FameStory.InterviewHard : FameStory.InterviewGood;
            Print(story, Save.Day);
            Logger?.Info("Standing: the " + venue + " interview is over (" + good + " answer(s) taken well, " + hard + " not); recognition " + _file.Recognition + ".");
        }

        internal void Declined() => Logger?.Info("Standing: the player walked on without a word for the reporter.");

        internal void Photographed()
        {
            if (!Running || Save == null)
            {
                return;
            }
            int day = Save.Day;
            FameRules.Publicity(_file, FamePress.PhotoPress, day, FameCause.Press);
            if (FameRules.MayNotice(_file, day))
            {
                Tell(NotificationType.Info, "reallife_fame_note_photo", null);
            }
            Logger?.Info("Standing: a photographer took the player's picture in the street.");
        }

        private void Invited(FameInvite invite)
        {
            Logger?.Info("Standing: an invitation (" + invite.Kind + ") for day " + invite.Day + " at " + invite.Hour + ":00, to be accepted by day " + invite.AnswerBy + ".");
            Tell(NotificationType.Info, "reallife_fame_note_invite", _openPage, "what", FameText.InviteName(invite.Kind), "day", FameText.N(invite.Day),
                "time", FameText.Hour(invite.Hour));
        }

        public void AnswerInvite(int id, bool accept)
        {
            FameInvite invite = Running ? _file.Invite(id) : null;
            if (invite == null || invite.State != FameInviteState.Offered)
            {
                return;
            }
            invite.State = accept ? FameInviteState.Accepted : FameInviteState.Declined;
            Logger?.Info("Standing: the invitation (" + invite.Kind + ", day " + invite.Day + ") is " + invite.State + ".");
        }

        // Nothing of the game's or the mod's holds the player, and they are not at the wheel.
        private static bool Free()
        {
            return LifeEvents.IsSafeForEvent() && !Wiring.Scenes.PlayerHeld && !PlayerHelper.IsUsingVehicle && !GameUi.TravelList.AnyOpen;
        }

        private void StartScene(FameInvite invite)
        {
            if (_scene != null || !_panel.Begin())
            {
                return;
            }
            _scene = invite;
            _sceneAt = 0;
            _sceneGood = _sceneHard = _scenePress = 0;
            if (invite.Kind == FameInviteKind.Show || invite.Kind == FameInviteKind.Magazine)
            {
                _venue = invite.Kind == FameInviteKind.Show ? FameVenue.Show : FameVenue.Magazine;
                _asks.Clear();
                FamePress.Questions(_file, _venue, invite.Id, _asks);
                Logger?.Info("Standing: the " + invite.Kind + " begins (" + _asks.Count + " questions).");
                ScenePage();
            }
            else
            {
                EveningBegin(invite);
            }
        }

        private void ScenePage()
        {
            bool show = _venue == FameVenue.Show;
            string title = FameText.InviteName(_scene.Kind);
            string place = FameText.T(show ? "reallife_fame_show_place" : "reallife_fame_magazine_place");
            string speaker = FameText.T(show ? "reallife_fame_show_host" : "reallife_fame_magazine_writer");
            if (_sceneAt >= _asks.Count)
            {
                string end = FameText.Fill(_sceneHard > _sceneGood ? "reallife_fame_scene_end_hard" : "reallife_fame_scene_end", "points", FameRules.Tenths(_scenePress));
                _panel.Show(title, place, speaker, end, new[] { FameText.T("reallife_fame_scene_leave") }, null, 1, _ => SceneOver(true));
                return;
            }
            FameAsk ask = _asks[_sceneAt];
            string[] keys = FameText.QuestionKeys(ask);
            string[] says = FameText.SayKeys(ask);
            string promise = FameText.PromiseWords(ask.Promise);
            string opening = _sceneAt == 0 ? (show ? "reallife_fame_show_open" : "reallife_fame_magazine_open") : "reallife_fame_scene_next";
            string body = FameText.Fill(opening, "question", FameText.Fill(keys[0], "promise", promise));
            string progress = FameText.Fill("reallife_fame_scene_progress", "place", place, "number", (_sceneAt + 1).ToString(), "of", _asks.Count.ToString());
            _panel.Show(title, progress, speaker, body, new[] { FameText.T(keys[1]), FameText.T(keys[2]) },
                new[] { FameText.Fill(says[0], "promise", promise), FameText.Fill(says[1], "promise", promise) }, 2, SceneAnswer);
        }

        private void SceneAnswer(int option)
        {
            if (_scene == null || _sceneAt >= _asks.Count)
            {
                return;
            }
            FameAnswer a = Answered(_asks[_sceneAt], option, _venue);
            _sceneGood += a.Good ? 1 : 0;
            _sceneHard += a.Good ? 0 : 1;
            _sceneAt++;
            ScenePage();
        }

        private void SceneOver(bool finished)
        {
            FameInvite invite = _scene;
            _scene = null;
            if (_panel.Active)
            {
                _panel.End();
            }
            if (invite == null)
            {
                return;
            }
            invite.State = FameInviteState.Done;
            if (invite.Kind == FameInviteKind.Show || invite.Kind == FameInviteKind.Magazine)
            {
                Interviewed(_venue, _sceneGood, _sceneHard + (finished ? 0 : 1));
            }
            else
            {
                EveningOver(invite, finished);
            }
        }

        // Every two seconds: an accident or a crime with a victim near the player, and whether they walked on without helping.
        private void WatchNeed(int day)
        {
            Vector3 where = Vector3.zero;
            bool need = FameBridges.Need != null && FameBridges.Need(out where);
            if (!need)
            {
                _sawNeed = false;   // over, or somebody helped
                return;
            }
            float distance = FameGame.Flat(PlayerHelper.GetPosition(), where);
            if (!_sawNeed)
            {
                if (distance < NeedNear && !PlayerHelper.IsUsingVehicle && !BuildingManager.IsInsideBuilding)
                {
                    _sawNeed = true;
                    _helped = false;
                    _needAt = where;
                }
                return;
            }
            if (_helped)
            {
                _sawNeed = false;
                return;
            }
            if (distance < NeedGone)
            {
                return;
            }
            _sawNeed = false;
            int witnesses = FameGame.PassersBy(_needAt, WitnessWithin);
            if (witnesses == 0 || !FamePress.RefusalMay(_file, day))
            {
                Logger?.Info("Standing: the player walked on from somebody who needed help; " + (witnesses == 0 ? "nobody saw." : "counted already today."));
                return;
            }
            bool reported = FamePress.Refused(_file, day, Random.Range(0, 1000));
            Logger?.Info("Standing: the player walked on from somebody who needed help, and " + witnesses + " saw it: integrity " + _file.Rep[(int)FamePart.Integrity]
                         + (reported ? ", and it will be in the paper." : "."));
            if (reported)
            {
                Print(FameStory.Refused, day);
            }
            if (FameRules.MayNotice(_file, day))
            {
                Tell(NotificationType.Warning, reported ? "reallife_fame_note_refused_reported" : "reallife_fame_note_refused", _openPage);
            }
        }

        /// <summary>The player helped somebody (told through Wiring/StandingLinks): walking on afterwards is no refusal.</summary>
        public void Helped() => _helped = true;
    }
}
