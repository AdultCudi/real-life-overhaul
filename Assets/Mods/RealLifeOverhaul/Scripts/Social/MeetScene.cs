using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Somebody walks up to the player in the street from out of view: a stranger who would like a word, a friend bringing
    /// somebody to be introduced, or a friend passing. They wait to be spoken to, talk, and walk off.
    /// </summary>
    public sealed class MeetScene : SocialScene
    {
        public enum Kind { Stranger, Introduction, DropBy }

        private enum Step { Approach, Offer, Talk, After, Leaving }

        private const float StopAt = 2.3f;         // metres from the player they stop at
        private const float TalkWithin = 3.4f;     // metres
        private const float ApproachMost = 75f;    // seconds they keep coming
        private const float Patience = 45f;        // seconds they wait to be spoken to, also outside a door the player went through
        private const float WalkMost = 45f;        // metres of route they walk in by at most

        private readonly Kind _kind;
        private readonly SocialPerson _who;        // the one the conversation is with: the stranger, or the friend who dropped by
        private readonly SocialPerson _friend;     // who brings them, for an introduction
        private readonly bool _atShop;             // a drop-by outside the player's own shop
        private CityPerson _a;                     // _who
        private CityPerson _b;                     // _friend
        private Step _step;
        private float _stepFrom;
        private float _startedAt;
        private float _nextAim;
        private Vector3 _aim;
        private bool _waved;
        private string _label;                     // what the marker on them says
        private string _heldLine;

        public MeetScene(Kind kind, SocialPerson who, SocialPerson friend = null, bool atShop = false)
        {
            _kind = kind;
            _who = who;
            _friend = friend;
            _atShop = atShop;
        }

        public SocialPerson Who => _who;

        protected override bool Begin(float now)
        {
            if (!D.OnFoot)
            {
                Refusal = TestRefusal.OnFootOnly;
                return false;
            }
            Vector3 player = D.PlayerPosition;
            if (!CitySpots.Pavement(player, 5f, out Vector3 here))
            {
                Refusal = TestRefusal.NoPavement;
                return false;
            }
            if (!D.Cast.HasRoom)
            {
                Refusal = TestRefusal.PoolBusy;
                return false;
            }
            if (!SocialPlaces.WalkIn(here, player, WalkMost, out Vector3 from, out _))
            {
                Refusal = TestRefusal.NoWayIn;
                return false;
            }
            _a = D.Cast.Put(_who, from, here);
            if (_a == null)
            {
                Refusal = TestRefusal.PoolBusy;
                return false;
            }
            if (!SocialCast.Walk(_a, here, out float length, out bool whole) || !whole || length > WalkMost * 1.3f)
            {
                // Their own agent does not get there by a short way after all.
                D.Cast.Release(_a);
                _a = null;
                Refusal = TestRefusal.NoWayIn;
                RefusalDetail = "the walk from the place found is " + (whole ? length.ToString("F0") + " m" : "not possible all the way");
                return false;
            }
            if (_kind == Kind.Introduction && _friend != null)
            {
                // Without room for two, the stranger comes alone.
                Vector3 beside = from + Vector3.Cross(Vector3.up, (here - from).normalized) * 1.1f;
                if (CitySpots.Pavement(beside, 1.2f, out Vector3 second) && CitySpots.ClearQuietly(second, 0.35f) && CitySpots.Reachable(second, here))
                {
                    _b = D.Cast.Put(_friend, second, here);
                }
            }
            _startedAt = now;
            Go(Step.Approach, now);
            _label = _kind == Kind.Stranger ? SocialLife.T("reallife_social_marker_somebody") : SocialLife.First(_friend ?? _who);
            Title = _kind == Kind.Stranger ? SocialLife.T("reallife_social_meet_title") : SocialLife.T("reallife_social_with", SocialLife.Data(_friend ?? _who));
            Line = SocialLife.T(_kind == Kind.Stranger ? "reallife_social_meet_coming" : _kind == Kind.DropBy ? "reallife_social_dropby_coming" : "reallife_social_intro_coming",
                SocialLife.Data(_friend ?? _who));
            Aim(now, true);
            MarkPerson(_a, _label);
            LogPut(_who, _a, here, "is walking up to the player (" + _kind + (_b != null ? ", with " + _friend.Name : string.Empty) + "): put out of view");
            return true;
        }

        private void Go(Step step, float now)
        {
            _step = step;
            _stepFrom = now;
        }

        // Pavement just short of the player, asked again at most once a second.
        private void Aim(float now, bool force)
        {
            if (!force && now < _nextAim)
            {
                return;
            }
            _nextAim = now + 1f;
            Vector3 player = D.PlayerPosition;
            Vector3 towards = _a.Position - player;
            towards.y = 0f;
            Vector3 want = towards.sqrMagnitude > 0.5f ? player + towards.normalized * (StopAt - 0.3f) : player;
            if (!CitySpots.Pavement(want, 2.5f, out Vector3 goal))
            {
                return;   // the player is off the pavement: they keep to the last place they could get to
            }
            if (!force && JobSpots.Flat(goal, _aim) < 0.7f)
            {
                return;
            }
            _aim = goal;
            _a.WalkTo(goal);
            if (_b != null && !_b.Lost)
            {
                Vector3 side = Vector3.Cross(Vector3.up, towards.sqrMagnitude > 0.01f ? towards.normalized : Vector3.forward) * 1.0f;
                if (CitySpots.Pavement(goal + side + towards.normalized * 0.5f, 1.5f, out Vector3 second))
                {
                    _b.WalkTo(second);
                }
            }
        }

        public override void Tick(float now)
        {
            if (_step == Step.Leaving)
            {
                if (!TickLeaving(now))
                {
                    Over = true;
                }
                return;
            }
            if (_a == null || _a.Lost)
            {
                Over = true;   // knocked down or gone
                return;
            }
            Vector3 player = D.PlayerPosition;
            float gap = JobSpots.Flat(_a.Position, player);
            if (_step == Step.Approach || _step == Step.Offer)
            {
                MarkPerson(_a, _label);
            }
            switch (_step)
            {
                case Step.Approach:
                    if (now - _startedAt > ApproachMost || !D.OnFoot)
                    {
                        GiveUp(now);
                        return;
                    }
                    if (gap <= StopAt + 0.4f || (_a.Arrived && gap < TalkWithin))
                    {
                        Arrive(now);
                        return;
                    }
                    Aim(now, _a.Arrived && now >= _nextAim);
                    break;
                case Step.Offer:
                    if (gap > TalkWithin + 2.5f)
                    {
                        // The player moved on: they follow, for as long as their patience lasts.
                        Prompt = null;
                        if (now - _stepFrom > Patience)
                        {
                            GiveUp(now);
                            return;
                        }
                        Aim(now, false);
                        if (_a.Arrived && gap < TalkWithin)
                        {
                            Arrive(now);
                        }
                        break;
                    }
                    if (now - _stepFrom > Patience || !D.OnFoot)
                    {
                        GiveUp(now);
                        return;
                    }
                    if (Prompt == null)
                    {
                        Offer(SocialLife.Say(_kind == Kind.Stranger ? "reallife_social_meet_prompt" : "reallife_social_talk_prompt", SocialLife.Data(_friend ?? _who)),
                            "reallife_social_button_talk", _a);
                    }
                    break;
                case Step.Talk:
                    if (TickTalk(now, _kind == Kind.Introduction && _b != null && !_b.Lost && Talk != null && !Talk.PersonSpeaks ? _b : _a))
                    {
                        Outcome(now);
                    }
                    break;
                case Step.After:
                    if (now - _stepFrom > 1.6f)
                    {
                        Depart(_a, now);
                        Depart(_b, now);
                        _a = null;
                        _b = null;
                        Line = null;
                        Title = null;
                        Go(Step.Leaving, now);
                    }
                    break;
            }
        }

        private void Arrive(float now)
        {
            _a.Stand();
            _a.Face(D.PlayerPosition);
            if (_b != null && !_b.Lost)
            {
                _b.Stand();
                _b.Face(D.PlayerPosition);
            }
            if (!_waved)
            {
                _waved = true;
                Gesture(_kind == Kind.Introduction && _b != null ? _b : _a, D.Props, "wave_hello");
                if (_kind == Kind.DropBy)
                {
                    Say(_who, SocialLines.Pick(SocialCatalogue.DropBy));
                }
            }
            Line = SocialLife.T(_kind == Kind.Stranger ? "reallife_social_meet_waiting" : "reallife_social_friend_waiting", SocialLife.Data(_friend ?? _who));
            if (_step != Step.Offer)
            {
                Go(Step.Offer, now);
            }
        }

        public override void Interact(float now)
        {
            if (_step != Step.Offer || Prompt == null || _a == null || _a.Lost)
            {
                return;
            }
            bool brought = _kind == Kind.Introduction && _b != null && !_b.Lost;   // the friend is here to make the introduction
            var talk = new SceneTalk
            {
                Person = _who, Introducer = brought ? _friend : null, Via = _kind == Kind.Introduction ? _friend : null,
                Mode = _kind == Kind.DropBy ? SceneTalk.DropBy : brought ? SceneTalk.Introduction : SceneTalk.Meet
            };
            if (_kind == Kind.DropBy)
            {
                bool shop = SocialTalk.Shop(out string name, out float sales, out _);
                string[] pool = !_atShop ? SocialCatalogue.DropByStreet : shop && sales > 0f ? SocialCatalogue.DropByShopGood : SocialCatalogue.DropByShopQuiet;
                talk.Opener = SocialLines.Pick(pool);
                talk.OpenerData = SocialLife.Data(_who, "shop", name ?? SocialLife.T("reallife_social_your_shop"));
                talk.OpenerData["amount"] = SocialLife.Money(sales);
                talk.GoLabel = "reallife_social_btn_good_to_see";
            }
            else
            {
                string[] pool = _kind == Kind.Introduction ? null : SocialCatalogue.MeetOpen[(int)_who.Origin];
                talk.Opener = pool != null ? SocialLines.Pick(pool) : "reallife_social_intro_hello";
                talk.OpenerData = SocialLife.Data(_who);
            }
            if (_b != null && !_b.Lost)
            {
                _b.Face(D.PlayerPosition);
            }
            Unmark();
            Converse(talk, _kind == Kind.Introduction && _b != null && !_b.Lost ? _b : _a, now);
            Go(Step.Talk, now);
        }

        private void Outcome(float now)
        {
            SceneTalk talk = Talk;
            Line = null;
            if (_kind == Kind.DropBy)
            {
                SocialLife.Contacted(_who);
                SocialLife.Remember(_who, MemoryCode.DroppedBy);
                _who.Shared++;
                SocialLife.Change(_who, 2f, talk != null && talk.Chatted ? 1f : 2f, 1f);
                SocialState.LastDropByDay = SocialLife.Day;
                D.Cast.Emoji(_a, CharacterEmojiName.CommonGreenHappy, 3f);
                Say(_who, SocialLines.Pick(_who.Together ? SocialCatalogue.PartingLove : SocialCatalogue.Parting));
            }
            else if (talk != null && talk.Swapped)
            {
                SocialLife.Meet(_who, talk.Warm, _kind == Kind.Introduction ? _friend : null);
                D.Cast.Emoji(_a, CharacterEmojiName.CommonGreenHappy, 3f);
                if (_friend != null)
                {
                    SocialLife.Contacted(_friend);
                    SocialLife.Remember(_friend, MemoryCode.Introduced);
                    SocialLife.Change(_friend, 1f, 2f, 1f);
                }
            }
            else if (talk != null && talk.Brushed)
            {
                SocialState.LastMeetDay = SocialLife.Day;   // nobody else tries for a while either
                D.Cast.Emoji(_a, CharacterEmojiName.CommonYellowNeutral, 3f);
                SocialLife.Toast(NotificationType.Info, SocialLines.Pick(SocialCatalogue.MeetBrushed), null, 6f);
                if (_friend != null)
                {
                    SocialLife.Change(_friend, 0f, SocialRules.RudeAffection, 0f);
                    SocialLife.Remember(_friend, MemoryCode.Rude);
                }
            }
            else
            {
                SocialState.LastMeetDay = SocialLife.Day;
                SocialLife.Toast(NotificationType.Info, "reallife_social_meet_no_numbers", null, 6f);
            }
            Gesture(_a, D.Props, ClipLibrary.Has("job_thank") ? "job_thank" : "wave_hello");
            Go(Step.After, now);
        }

        private void GiveUp(float now)
        {
            Prompt = null;
            Line = null;
            Unmark();
            ToldGone();
            SocialDirector.Logger?.Info("Social: " + _who.Name + " gave up (" + _step + " for " + (now - _stepFrom).ToString("F0") + " s, "
                                        + (D.OnFoot ? "the player on foot" : "the player in a vehicle") + ", "
                                        + JobSpots.Flat(_a.Position, D.PlayerPosition).ToString("F0") + " m apart) and walked off.");
            Go(Step.After, now - 2f);
        }

        private void ToldGone()
        {
            if (_kind == Kind.Stranger)
            {
                SocialLife.Toast(NotificationType.Info, SocialLines.Pick(SocialCatalogue.MeetGaveUp), null, 6f);
            }
            else
            {
                SocialLife.Toast(NotificationType.Info, "reallife_social_meet_missed", SocialLife.Data(_friend ?? _who), 7f);
            }
        }

        public override bool Hold(SceneBreak why, float now)
        {
            if ((_step != Step.Approach && _step != Step.Offer) || why == SceneBreak.TimeSkip || _a == null || _a.Lost)
            {
                return false;
            }
            // They wait outside the door as long as they would have waited to be spoken to.
            Prompt = null;
            _a.Stand();
            if (_b != null && !_b.Lost)
            {
                _b.Stand();
            }
            _heldLine = Line;
            Line = SocialLife.T(_kind == Kind.Stranger ? "reallife_social_meet_held" : "reallife_social_obj_held", SocialLife.Data(_friend ?? _who));
            return true;
        }

        public override bool Holding(SceneBreak why, float now, float held)
        {
            if (held <= Patience)
            {
                return true;
            }
            ToldGone();
            SocialDirector.Logger?.Info("Social: " + _who.Name + " gave up waiting outside after " + held.ToString("F0") + " s (" + why + ").");
            return false;
        }

        public override void Resume(SceneBreak why, float now, float held)
        {
            Line = _heldLine;
            _heldLine = null;
            _startedAt += held;
            _stepFrom += held;
            if (_a != null && !_a.Lost && _step == Step.Approach)
            {
                Aim(now, true);
            }
        }

        public override void Cut(SceneBreak why)
        {
            string reason = WhyText(why);
            if (reason != null && (_step == Step.Approach || _step == Step.Offer))
            {
                SocialLife.Toast(NotificationType.Info, "reallife_social_meet_cut", SocialLife.Data(_friend ?? _who, "why", reason), 8f);
            }
        }

        public override string Describe()
        {
            return "meeting (" + _kind + ") with " + _who.Name + ", step " + _step;
        }

        public override void End(SceneEnd why)
        {
            SocialDirector.Talk = null;
            D.Cast.Release(_a);
            D.Cast.Release(_b);
            ReleaseLeaving();
            _a = null;
            _b = null;
        }
    }
}
