using BigAmbitions.Characters.Appearance;
using BigAmbitions.DayNightCycle;
using Entities;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The ring and the question. A ring is bought at a real jeweller's counter; the question is
    /// asked wherever the player chooses, and where and when it is asked matters: an evening, a
    /// dry sky, the end of a good night out, being dressed for it all help, and so does the
    /// ring. They may say "not yet"; nothing is lost but a week.
    /// </summary>
    internal static class Courtship
    {
        private static readonly string[] RingNames =
        {
            null, "reallife_family_ring_1", "reallife_family_ring_2", "reallife_family_ring_3", "reallife_family_ring_4"
        };

        private static readonly string[] YesLines = { "reallife_family_propose_yes_1", "reallife_family_propose_yes_2", "reallife_family_propose_yes_3", "reallife_family_propose_yes_4" };
        private static readonly AppearanceTag[] Formal = { AppearanceTag.Formal };
        private static int _lastYes = -1;

        public static string RingName(int tier)
        {
            return RingNames[Mathf.Clamp(tier, 1, 4)];
        }

        /// <summary>At the jeweller's counter: one ring at a time, with its price.</summary>
        public static DialogEntry Rings(int index)
        {
            int tier = 1 + (index % 4 + 4) % 4;
            FamilySave s = FamilyState.S;
            var fill = Args.Us.Cost(FamilyBalance.Ring(tier)).With(FamilyText.T(RingNames[tier]));
            string key = s.Ring == tier ? "reallife_family_rings_have" : s.Ring > 0 ? "reallife_family_rings_offer_again" : "reallife_family_rings_offer";
            return FamilyTalk.Either(FamilyText.Say(key, fill), "reallife_family_rings_buy", () => Buy(tier), "reallife_family_rings_next", () => Rings(index + 1));
        }

        private static DialogEntry Buy(int tier)
        {
            FamilySave s = FamilyState.S;
            if (s.Ring == tier)
            {
                return FamilyTalk.Close("reallife_family_rings_already");
            }
            if (!FamilyTalk.Pay(FamilyBalance.Ring(tier), "reallife_transaction_family_ring"))
            {
                return null;
            }
            FamilyState.MoneyOut(FamilyBalance.Ring(tier), 4);
            s.Ring = tier;
            return FamilyTalk.Close("reallife_family_rings_bought", Args.Us.With(FamilyText.T(RingNames[tier])));
        }

        /// <summary>Whether the two are close enough, for long enough, for the question to be welcome at all.</summary>
        public static bool Ready()
        {
            if (Partner.Kept(out PersonInfo p) && p.Stage >= 5)
            {
                return Partner.DaysTogether >= FamilyBalance.ProposeDaysTogether;
            }
            return Partner.Affection >= FamilyBalance.ProposeAffection && Partner.DaysTogether >= FamilyBalance.ProposeDaysTogether;
        }

        /// <summary>Their answer. A "not yet" comes with what would have helped.</summary>
        public static bool Answer(bool test, bool indoors, out string reason)
        {
            FamilySave s = FamilyState.S;
            reason = "reallife_family_propose_no_not_yet";
            if (!test && !Ready())
            {
                reason = "reallife_family_propose_no_too_soon";
                return false;
            }
            int day = TimeHelper.CurrentDay;
            int hour = TimeHelper.CurrentHour;
            float odds = 0.42f + Mathf.Clamp((Partner.Affection - FamilyBalance.ProposeAffection) * 0.012f, -0.1f, 0.3f)
                         + Mathf.Clamp((s.Harmony - 60f) * 0.004f, -0.2f, 0.16f) + FamilyBalance.RingOdds[Mathf.Clamp(s.Ring, 0, 4)];
            if (Partner.DaysTogether >= 30)
            {
                odds += 0.08f;
            }
            float moment = 0f;
            if (hour >= 17 && hour < 23) moment += 0.08f;
            if (hour < 6) moment -= 0.15f;
            if (TimeHelper.IsWeekend) moment += 0.04f;
            if (!indoors)
            {
                moment += RainHelper.AreRainDropsFalling ? -0.08f : 0.04f;
            }
            if (s.LastDateDay == day) moment += 0.10f;
            if (DressedUp()) moment += 0.05f;
            bool rough = s.LastArgueDay > 0 && day - s.LastArgueDay <= 2;
            if (rough) odds -= 0.20f;
            odds = Mathf.Clamp(odds + moment, 0.05f, 0.97f);
            if (test)
            {
                odds = Mathf.Max(odds, 0.9f);
            }
            bool yes = Random.value < odds;
            if (!yes)
            {
                reason = rough ? "reallife_family_propose_no_rough" : moment < 0f ? "reallife_family_propose_no_moment" : "reallife_family_propose_no_not_yet";
            }
            FamilyDirector.Logger?.Info(System.FormattableString.Invariant($"Family: proposal answered {(yes ? "yes" : "not yet")} (odds {odds:F2}, moment {moment:F2}, ring {s.Ring})."));
            return yes;
        }

        /// <summary>True if the player is wearing clothes the game calls formal.</summary>
        public static bool DressedUp()
        {
            try
            {
                PlayerController player = PlayerHelper.PlayerController;
                return player != null && player.Character != null && player.Character.appearanceSetter != null
                       && player.Character.appearanceSetter.IsWearingClothesWithTags(Formal);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        public static string YesLine()
        {
            return FamilyText.Pick(YesLines, ref _lastYes);
        }

        public static void Engage(bool announce)
        {
            FamilySave s = FamilyState.S;
            FamilyState.Bond = Bond.Engaged;
            s.EngagedDay = TimeHelper.CurrentDay;
            s.InLaws = true;
            FamilyPhone.SetupInLaws();
            FamilyState.Harmony(10f);
            FamilyState.Remember("reallife_family_memory_engaged", Partner.Name);
            Partner.Tell(FamilyEvent.Engaged, 8f, 8f);
            FamilyModifiers.Remove(FamilyModifiers.Together);
            FamilyModifiers.Add(FamilyModifiers.Engaged);
            Relatives.MomSays("reallife_family_mom_engaged");
            Relatives.InLawsSay("reallife_family_inlaws_welcome");
            if (announce)
            {
                FamilyText.Good("reallife_family_engaged", Args.Us);
            }
            FamilyDirector.Logger?.Info("Family: engaged.");
        }

        public static void Declined()
        {
            FamilySave s = FamilyState.S;
            s.LastProposalDay = TimeHelper.CurrentDay;
            Partner.Tell(FamilyEvent.ProposalDeclined, -1f);
        }

        /// <summary>The engagement is called off: the two are a couple again, or apart, as things stand.</summary>
        public static void CallOff()
        {
            FamilySave s = FamilyState.S;
            WeddingPlanner.Cancel(false);
            FamilyState.Bond = Bond.Dating;
            FamilyState.Harmony(-25f);
            FamilyModifiers.Remove(FamilyModifiers.Engaged);
            FamilyModifiers.Add(FamilyModifiers.Heartache);
            FamilyState.Remember("reallife_family_memory_engagement_off", Partner.Name);
            Partner.Tell(FamilyEvent.WeddingCancelled, -15f, -10f);
            s.LastProposalDay = TimeHelper.CurrentDay;
        }
    }

    /// <summary>
    /// The proposal, wherever the player chose. Out of doors the partner walks in from out of
    /// view along the pavement; at home they are already there. They come to stand in front of
    /// the player, the player goes down on one knee when they choose to, and the answer is
    /// given face to face. Every way out gives the partner back (to the home, or off down the
    /// street and out of view) and frees the player.
    /// </summary>
    internal sealed class ProposalScene : FamilyScene
    {
        private enum Step { Coming, Waiting, Stepping, GoingDown, Kneeling, Reacting, Answer, Parting }

        private const float PartnerAhead = 1.25f;   // metres in front of the kneeler that the proposal's clips put the partner's face (CityAssets)

        private static readonly string[] Questions = { "reallife_family_propose_ask_1", "reallife_family_propose_ask_2", "reallife_family_propose_ask_3" };
        private static int _lastQuestion = -1;

        private readonly bool _test;
        private FamilyDirector _d;
        private CityPerson _partner;
        private bool _fromHome;
        private bool _indoors;
        private Step _step;
        private Vector3 _goal;
        private float _until;
        private float _giveUp;
        private float _nextAim;
        private bool _yes;
        private string _reason;
        private bool _talkClosed;
        private bool _answered;
        private bool _ringClips;         // kneeling with the proposal's own clips, the ring box in the hand

        private readonly System.Action _kneel;

        public ProposalScene(bool test)
        {
            _test = test;
            _kneel = Kneel;
        }

        public override string Name => "the proposal";

        public override bool Begin(FamilyDirector director, out string refusal)
        {
            _d = director;
            FamilySave s = FamilyState.S;
            refusal = null;
            if (FamilyState.Bond != Bond.Dating)
            {
                refusal = FamilyState.Together ? "reallife_family_no_already_engaged" : "reallife_family_no_partner";
                return false;
            }
            if (s.Ring <= 0)
            {
                refusal = "reallife_family_no_ring";
                return false;
            }
            if (!_test && s.LastProposalDay > 0 && TimeHelper.CurrentDay - s.LastProposalDay < FamilyBalance.ProposeAgainDays)
            {
                refusal = "reallife_family_no_propose_again";
                return false;
            }
            Vector3 player = PlayerHelper.GetPosition();
            _indoors = BuildingManager.IsInsideBuilding;
            float now = Time.time;
            if (HomeLife.Present)
            {
                _partner = HomeLife.Hand();
                _fromHome = true;
                _goal = _partner.Position;
                _d.Cast.Face(_partner, player);
                _step = Step.Waiting;
                _giveUp = now + 180f;
                return true;
            }
            if (_indoors)
            {
                refusal = "reallife_family_no_propose_indoors";
                return false;
            }
            if (_d.Cast.Failed || !Near(player, out _goal))
            {
                refusal = "reallife_family_no_room_here";
                return false;
            }
            Vector3 from = default;
            bool found = false;
            for (int i = 0; i < 4 && !found; i++)
            {
                found = CitySpots.PavementRing(_goal, 24f, 46f, true, out from) && CitySpots.Reachable(from, _goal);
            }
            if (!found)
            {
                refusal = "reallife_family_no_way_in";
                return false;
            }
            _partner = _d.Cast.Put(0, FamilyLook.Partner, from, _goal, NavMeshHelperIds.Npc);
            if (_partner == null || !_partner.WalkTo(_goal))
            {
                refusal = "reallife_family_no_room_here";
                return false;
            }
            _step = Step.Coming;
            _giveUp = now + 90f;
            FamilyText.Toast(NotificationType.Info, "reallife_family_propose_coming", Args.Us, 7f);
            return true;
        }

        // A clear piece of pavement a step or two from the player, in front of them for preference.
        private static bool Near(Vector3 player, out Vector3 spot)
        {
            PlayerController controller = PlayerHelper.PlayerController;
            Vector3 forward = controller != null ? controller.Character.transform.forward : Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i % 2 == 0 ? i * 22.5f : -(i + 1) * 22.5f, 0f) * forward;
                if (CitySpots.Stand(player + direction * 1.5f, 0.4f, out spot))
                {
                    return true;
                }
            }
            spot = default;
            return false;
        }

        public override void Tick(float now)
        {
            if (_partner == null || _partner.Lost || PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding != _indoors
                || (_fromHome && !FamilyPlaces.AtHome()))
            {
                if (!_answered)
                {
                    FamilyText.Toast(NotificationType.Info, "reallife_family_propose_another_time", Args.Us, 6f);
                }
                Done = true;
                return;
            }
            Vector3 player = PlayerHelper.GetPosition();
            switch (_step)
            {
                case Step.Coming:
                    if (_partner.Arrived && _partner.Near(_goal, 0.8f))
                    {
                        _partner.Stand();
                        _d.Cast.Face(_partner, player);
                        _step = Step.Waiting;
                        _giveUp = now + 180f;
                    }
                    else if (now >= _giveUp)
                    {
                        FamilyText.Toast(NotificationType.Info, "reallife_family_propose_another_time", Args.Us, 6f);
                        Done = true;
                    }
                    else if (now >= _nextAim && CitySpots.Flat(player - _goal) > 5f * 5f)
                    {
                        // The player walked on: they follow, to a place beside where the player is now.
                        _nextAim = now + 3f;
                        if (Near(player, out Vector3 again) && _partner.WalkTo(again))
                        {
                            _goal = again;
                        }
                    }
                    break;
                case Step.Waiting:
                    if (now >= _giveUp)
                    {
                        FamilyText.Toast(NotificationType.Info, "reallife_family_propose_waited", Args.Us, 7f);
                        _step = Step.Parting;
                        _talkClosed = true;
                        Part(now);
                        break;
                    }
                    if (now >= _nextAim)
                    {
                        _nextAim = now + 1f;
                        _d.Cast.Face(_partner, player);
                    }
                    if (CitySpots.Flat(player - _partner.Position) < 4f * 4f)
                    {
                        _d.Prompt("reallife_family_prompt_kneel", Partner.Name, _kneel, _partner.Valid ? _partner.Character.transform : null);
                    }
                    else if (!_fromHome && CitySpots.Flat(player - _partner.Position) > 9f * 9f)
                    {
                        _step = Step.Coming;
                        _giveUp = now + 60f;
                        _nextAim = 0f;
                    }
                    break;
                case Step.Stepping:
                    // Come to a stop on the spot (an agent reads "arrived" a quarter of a metre early), or held up.
                    if ((_partner.Arrived && _partner.Near(_goal, 0.5f) && _partner.Speed < 0.05f) || now >= _until)
                    {
                        _partner.Stand();
                        _d.Cast.Face(_partner, player);
                        Down(now, player);
                    }
                    break;
                case Step.GoingDown:
                    if (now >= _until)
                    {
                        Ask(now);
                    }
                    break;
                case Step.Kneeling:
                    if (now >= _until)
                    {
                        _yes = Courtship.Answer(_test, _indoors, out _reason);
                        _answered = true;
                        if (!_partner.Work(_yes ? "propose_react_yes" : "propose_react_unsure", _d.Cast.Props, 0.3f))
                        {
                            _partner.Work(_yes ? "clap" : "talk_a", _d.Cast.Props, 0.3f);
                        }
                        _step = Step.Reacting;
                        _until = now + 2.2f;
                    }
                    break;
                case Step.Reacting:
                    if (now >= _until)
                    {
                        // Up off the knee with the box still in the hand (propose_kneel_up, from the loop's first pose to
                        // standing); without that clip, a fade back to standing as before.
                        _step = Step.Answer;
                        if (_ringClips && ClipLibrary.Has("propose_kneel_up") && ClipLibrary.TryGet("propose_kneel_up", out CityClip up)
                            && _d.Body.Clip("propose_kneel_up", 0.25f))
                        {
                            _until = now + up.Length;
                        }
                        else
                        {
                            _d.Body.Rest(0.4f);
                            _until = now + 1.2f;
                        }
                    }
                    break;
                case Step.Answer:
                    if (now >= _until)
                    {
                        _d.Body.Rest(0.3f);   // the box put away (nothing to do if they stood up by the fade)
                        _d.Body.Free();
                        _partner.Work("talk_a", _d.Cast.Props, 0.3f);
                        if (_yes)
                        {
                            Courtship.Engage(false);
                        }
                        else
                        {
                            Courtship.Declined();
                        }
                        string line = _yes ? Courtship.YesLine() : _reason;
                        _step = Step.Parting;
                        _talkClosed = false;
                        _giveUp = now + 3f;
                        if (!FamilyTalk.Open(() => FamilyTalk.Close(line), () => _talkClosed = true, _partner.Character))
                        {
                            FamilyText.Toast(_yes ? NotificationType.Success : NotificationType.Info, line, Args.Us, 10f);
                            _talkClosed = true;
                        }
                    }
                    break;
                case Step.Parting:
                    if (!_talkClosed && DialogController.current == null && now >= _giveUp)
                    {
                        _talkClosed = true;
                    }
                    if (_talkClosed)
                    {
                        Part(now);
                    }
                    break;
            }
        }

        // The player pressed the key: held where they stand; the partner steps round in front of them.
        private void Kneel()
        {
            if (_step != Step.Waiting || _partner == null || _partner.Lost)
            {
                return;
            }
            float now = Time.time;
            _d.Body.Bind(PlayerHelper.PlayerController);
            _d.Body.Hold();
            PlayerController controller = PlayerHelper.PlayerController;
            Vector3 player = controller.transform.position;
            Vector3 forward = controller.Character.transform.forward;
            forward.y = 0f;
            Vector3 front = player + forward.normalized * PartnerAhead;
            bool room = _indoors ? FamilyPlaces.Floor(front, player, 0.25f, 0.3f, out front, out _) : CitySpots.OnPavement(front, out front) && CitySpots.ClearQuietly(front, 0.3f);
            if (room && CitySpots.Flat(_partner.Position - front) > 0.45f * 0.45f && _partner.WalkTo(front))
            {
                _goal = front;
                _step = Step.Stepping;
                _until = now + 7f;
                return;
            }
            _d.Cast.Face(_partner, player);
            Down(now, player);
        }

        private void Down(float now, Vector3 player)
        {
            // Facing each other: the partner has turned to the player; the player turns to them if they are not in front already.
            PlayerController controller = PlayerHelper.PlayerController;
            Vector3 to = _partner.Position - player;
            to.y = 0f;
            if (controller != null && to.sqrMagnitude > 0.04f && Vector3.Dot(controller.Character.transform.forward, to.normalized) < 0.9f)
            {
                controller.Character.transform.rotation = Quaternion.LookRotation(to);
            }
            // Down on one knee with the ring box open on the right palm (it comes with the clips, at their grip, and stays
            // in the hand from the first frame of going down to the last of getting up): propose_kneel_down once, held,
            // then the kneeling loop the moment it is over (JobBody.Chain; the director ticks the body). The question is
            // asked when the box has come up (the clip's "present" mark). A bundle without the way down fades into the
            // loop from standing, as before; one without the loop uses the mod's working kneel, with nothing in the hand.
            _ringClips = _d.Body.Chain("propose_kneel_down", "propose_kneel", 0.3f);
            if (_ringClips)
            {
                _step = Step.GoingDown;
                _until = now + (ClipLibrary.TryGet("propose_kneel_down", out CityClip down) ? down.Mark("present", down.Length) : 2.5f);
                return;
            }
            _ringClips = _d.Body.Clip("propose_kneel", 0.35f);
            if (!_ringClips)
            {
                _d.Body.Clip(SceneClips.Kneel, 0.35f);
            }
            Ask(now);
        }

        private void Ask(float now)
        {
            FamilyText.Toast(NotificationType.Info, FamilyText.Pick(Questions, ref _lastQuestion), Args.Us, 6f);
            _step = Step.Kneeling;
            _until = now + 3.4f;
        }

        private float _leaveBy;
        private bool _leaving;

        private void Part(float now)
        {
            if (_fromHome)
            {
                _partner.Idle(0.3f);
                HomeLife.TakeBack(_partner, now);
                _partner = null;
                Done = true;
                return;
            }
            if (!_leaving)
            {
                _leaving = true;
                _leaveBy = now + 45f;
                _partner.Idle(0.3f);
                if (!CitySpots.PavementRing(_partner.Position, 28f, 50f, false, out Vector3 away) || !_partner.WalkTo(away))
                {
                    _leaveBy = now + 12f;
                }
                return;
            }
            // Off down the street: gone once out of view, or after long enough.
            Vector3 player = PlayerHelper.GetPosition();
            bool far = CitySpots.Flat(_partner.Position - player) > 22f * 22f;
            bool unseen = !CitySpots.InView(_partner.Position);
            if ((far && unseen) || (now >= _leaveBy && unseen) || now >= _leaveBy + 40f)
            {
                Done = true;
            }
        }

        public override void End()
        {
            if (_d == null)
            {
                return;
            }
            _d.Body.Release();
            if (_partner != null)
            {
                if (_fromHome && !_partner.Lost && FamilyPlaces.AtHome())
                {
                    _partner.Idle(0.2f);
                    HomeLife.TakeBack(_partner, Time.time);
                }
                else
                {
                    _d.Cast.Release(_partner);
                }
                _partner = null;
            }
        }
    }
}
