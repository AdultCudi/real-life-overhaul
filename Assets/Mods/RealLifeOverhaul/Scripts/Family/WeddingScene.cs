using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The ceremony out of doors at the venue, set out under a cut to black on spots tested for room: the officiant, guests in
    /// rows and what was paid for. The aisle, vows with a choice, rings, applause, a photograph and the first dance; then the
    /// reception under a fade, and everybody taken away unseen.
    /// </summary>
    internal sealed class WeddingScene : FamilyScene
    {
        /// <summary>The test button's wedding: it costs nothing and brings in nothing.</summary>
        public bool Free;

        private enum Step { Gathering, Aisle, Welcome, Vows, TheirVow, Rings, Pronounce, Applause, Photo, Dance, Leaving, Cut }

        private const int MaxGuestSpots = 6;
        private const int MaxBorrowed = 4;                  // guests borrowed from the living city
        private const float RingGap = 1.10f;                // metres apart, facing, that ring_present and ring_receive are made for
        private const float RingSlack = 0.12f;              // metres either way
        private const string LeadClip = "slow_dance_a";     // the player's half of the first dance
        private const string FollowClip = "slow_dance_b";   // the partner's, facing the leader, started at the same moment
        private const float DanceGap = 0.45f;               // metres apart, facing, that the two dance clips are made for
        private const float DanceSlack = 0.10f;             // metres: further off, the clasped hands would be apart
        private const float DanceSeconds = 8f;              // one turn of the loop

        private static readonly string[] HeartVows = { "reallife_family_vow_heart_1", "reallife_family_vow_heart_2", "reallife_family_vow_heart_3" };
        private static readonly string[] FunnyVows = { "reallife_family_vow_funny_1", "reallife_family_vow_funny_2", "reallife_family_vow_funny_3" };
        private static readonly string[] TheirVows = { "reallife_family_vow_theirs_1", "reallife_family_vow_theirs_2", "reallife_family_vow_theirs_3", "reallife_family_vow_theirs_4" };
        private static int _lastHeart = -1, _lastFunny = -1, _lastTheirs = -1;

        private readonly Vector3[] _guestSpots = new Vector3[MaxGuestSpots];
        private readonly List<CityPerson> _borrowed = new List<CityPerson>(MaxBorrowed);
        private readonly List<Transform> _dressing = new List<Transform>(12);
        private FamilyDirector _d;
        private CityPeople _city;
        private CityPerson _partner, _officiant, _guest;
        private Vector3 _anchor, _dir, _right, _front, _mine, _theirs, _officiantSpot, _foot;
        private int _guestCount;
        private Step _step;
        private float _until;
        private float _giveUp;
        private int _vow;            // 0 not chosen yet, 1 from the heart, 2 to make them laugh
        private float _quietSince;
        private bool _setUp;
        private bool _released;
        private bool _married;
        private bool _closing;       // the partner is stepping in to the clips' distance
        private float _closeFrom;
        private bool _danced;        // the first dance has been offered, done or not: once
        private bool _dancing;

        public override string Name => "the wedding";

        public override bool Begin(FamilyDirector director, out string refusal)
        {
            _d = director;
            refusal = "reallife_family_no_room_wedding";
            if (FamilyState.Bond != Bond.Engaged || !FamilyState.S.Wedding.Active)
            {
                refusal = "reallife_family_no_not_engaged";
                return false;
            }
            Vector3 player = PlayerHelper.GetPosition();
            if (director.Cast.Failed || !CitySpots.Pavement(player, 3f, out _anchor))
            {
                return false;
            }
            PlayerController controller = PlayerHelper.PlayerController;
            Vector3 facing = controller.Character.transform.forward;
            facing.y = 0f;
            facing = facing.sqrMagnitude > 0.01f ? facing.normalized : Vector3.forward;
            bool along = CitySpots.Along(_anchor, out Vector3 run);
            bool laid = false;
            // Along the pavement the way the player faces, then the other way, then across it.
            for (int i = 0; i < 6 && !laid; i++)
            {
                Vector3 dir;
                switch (i)
                {
                    case 0: dir = along ? (Vector3.Dot(run, facing) >= 0f ? run : -run) : facing; break;
                    case 1: dir = along ? (Vector3.Dot(run, facing) >= 0f ? -run : run) : -facing; break;
                    case 2: dir = facing; break;
                    case 3: dir = Vector3.Cross(Vector3.up, along ? run : facing); break;
                    case 4: dir = -Vector3.Cross(Vector3.up, along ? run : facing); break;
                    default: dir = -facing; break;
                }
                laid = Lay(dir.normalized, 6f) || Lay(dir.normalized, 4.5f);
            }
            if (!laid)
            {
                return false;
            }
            _city = director.GetComponent<CityPeople>();
            _step = Step.Gathering;
            _giveUp = Time.time + 420f;
            if (!director.Run(SetOut()))
            {
                refusal = "reallife_family_no_busy";
                return false;
            }
            refusal = null;
            return true;
        }

        // The places everybody stands for an aisle running from the player towards dir. False unless the essentials all have room.
        private bool Lay(Vector3 dir, float length)
        {
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Vector3 front = _anchor + dir * length;
            if (!CitySpots.Stand(front - right * 0.55f, 0.32f, out Vector3 mine) || !CitySpots.Stand(front + right * 0.55f, 0.32f, out Vector3 theirs)
                || !CitySpots.Stand(front + dir * 1.25f, 0.35f, out Vector3 officiant))
            {
                return false;
            }
            if (!CitySpots.Stand(_anchor + right * 1.0f, 0.35f, out Vector3 foot) && !CitySpots.Stand(_anchor + right * 1.0f + dir * 0.9f, 0.35f, out foot)
                && !CitySpots.Stand(_anchor + dir * 1.2f + right * 0.6f, 0.35f, out foot))
            {
                return false;
            }
            if (!CitySpots.Reachable(foot, theirs) || !CitySpots.Reachable(_anchor, mine) || Mathf.Abs(front.y - _anchor.y) > 0.6f)
            {
                return false;
            }
            _dir = dir;
            _right = right;
            _front = front;
            _mine = mine;
            _theirs = theirs;
            _officiantSpot = officiant;
            _foot = foot;
            _guestCount = 0;
            for (int row = 0; row < 3 && _guestCount < MaxGuestSpots; row++)
            {
                float back = 2.3f + row * 1.3f;
                if (back > length - 1.6f)
                {
                    break;
                }
                for (int side = 0; side < 4 && _guestCount < MaxGuestSpots; side++)
                {
                    float across = (side < 2 ? 1.45f : 2.35f) * (side % 2 == 0 ? -1f : 1f);
                    if (CitySpots.Stand(front - dir * back + right * across, 0.35f, out Vector3 spot) && CitySpots.Flat(spot - foot) > 1.2f * 1.2f)
                    {
                        _guestSpots[_guestCount++] = spot;
                    }
                }
            }
            return true;
        }

        private IEnumerator SetOut()
        {
            yield return UiFader.Fade(0.5f, "reallife_family_passage_gathering");
            Place();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return UiFader.UnFade();
            _setUp = true;
        }

        private void Place()
        {
            WeddingPlan plan = FamilyState.S.Wedding;
            FamilyCast cast = _d.Cast;
            HomeLife.Leave();
            _partner = cast.Put(0, FamilyLook.PartnerFormal, _foot, _front, NavMeshHelperIds.Npc);
            _officiant = cast.Put(1, FamilyLook.Officiant, _officiantSpot, _front - _dir, NavMeshHelperIds.Npc);
            int next = 0;
            if (_guestCount > 0)
            {
                _guest = cast.Put(2, PhoneContacts.Mom != null ? FamilyLook.Mother : FamilyLook.Guest, _guestSpots[next], _front, NavMeshHelperIds.Npc);
                if (_guest != null)
                {
                    next++;
                }
            }
            // A few more from the living city's own people, when it has any to spare, in the clothes they stand in.
            int wanted = plan.Invited ? (plan.Tier == 0 ? 1 : plan.Tier == 1 ? 3 : MaxBorrowed) : 0;
            while (_city != null && Settings.CityLife && next < _guestCount && _borrowed.Count < wanted && _city.Free > 0)
            {
                CityPerson person = _city.Take(CityLook.Civilian, _guestSpots[next], _front, false);
                next++;
                if (person != null)
                {
                    _borrowed.Add(person);
                }
            }
            // What was paid for is what is there.
            if (plan.Tier >= 1 && CitySpots.OnPavement(_front + _dir * 2.0f, out Vector3 arch))
            {
                Dress(FamilyProps.Arch, arch, -_dir);
            }
            if (plan.Flowers)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 at = _front - _dir * (i < 2 ? 0.2f : 2.0f) + _right * (i % 2 == 0 ? -1.15f : 1.15f) * (i < 2 ? 1.25f : 0.75f);
                    if (i >= 2 && _guestCount == 0)
                    {
                        break;
                    }
                    if (CitySpots.Stand(at, 0.28f, out Vector3 stand) && Apart(stand))
                    {
                        Dress(FamilyProps.Flowers, stand, -_dir);
                    }
                }
            }
            if (plan.Tier >= 2)
            {
                float length = Vector3.Dot(_front - _anchor, _dir);
                for (float along = 1.2f; along < length - 0.8f; along += 1f)
                {
                    if (CitySpots.OnPavement(_anchor + _dir * along, out Vector3 strip) && Mathf.Abs(strip.y - _anchor.y) < 0.15f)
                    {
                        Dress(FamilyProps.Runner, strip, _dir);
                    }
                }
            }
            FamilyDirector.Logger?.Info($"Family: wedding set out (partner {_partner != null}, officiant {_officiant != null}, family guest {_guest != null}, "
                                        + $"{_borrowed.Count} more guests of {_guestCount} places, {_dressing.Count} props).");
        }

        // Not where anybody stands or walks.
        private bool Apart(Vector3 spot)
        {
            if (CitySpots.Flat(spot - _mine) < 0.8f * 0.8f || CitySpots.Flat(spot - _theirs) < 0.8f * 0.8f || CitySpots.Flat(spot - _officiantSpot) < 0.8f * 0.8f
                || CitySpots.Flat(spot - _foot) < 0.9f * 0.9f)
            {
                return false;
            }
            for (int i = 0; i < _guestCount; i++)
            {
                if (CitySpots.Flat(spot - _guestSpots[i]) < 0.8f * 0.8f)
                {
                    return false;
                }
            }
            return true;
        }

        private void Dress(string id, Vector3 at, Vector3 facing)
        {
            Transform prop = _d.Props.Stand(id, at, facing);
            if (prop != null)
            {
                _dressing.Add(prop);
            }
        }

        public override void Tick(float now)
        {
            if (_step == Step.Cut)
            {
                Done = !_d.Busy;
                return;   // the screen is dark
            }
            if (!_setUp)
            {
                if (!_d.Busy)
                {
                    // Setting out was cut off before it finished: nothing is left standing, and the ceremony is told instead.
                    Release();
                    Done = true;
                    WeddingDay.Narrated();
                }
                return;
            }
            Vector3 player = PlayerHelper.GetPosition();
            if (_step != Step.Leaving && (_partner == null || _partner.Lost || _officiant == null || _officiant.Lost))
            {
                if (_step == Step.Gathering && (_partner == null || _officiant == null))
                {
                    // Nobody could be put out at all: told rather than shown.
                    Release();
                    Done = true;
                    WeddingDay.Narrated();
                    return;
                }
                Abandon("reallife_family_wedding_interrupted");
                return;
            }
            if (_step != Step.Leaving && (PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding || CitySpots.Flat(player - _front) > 45f * 45f || now >= _giveUp))
            {
                Abandon("reallife_family_wedding_walked_off");
                return;
            }
            switch (_step)
            {
                case Step.Gathering:
                    _partner.WalkTo(_theirs);
                    _step = Step.Aisle;
                    break;
                case Step.Aisle:
                    _d.Objective("reallife_family_objective_title", "reallife_family_objective_aisle", Partner.Name);
                    bool there = _partner.Arrived && _partner.Near(_theirs, 0.6f);
                    if (there && _partner.Playing == null && _partner.Speed < 0.05f)
                    {
                        _partner.Stand();
                        _d.Cast.Face(_partner, _mine);
                    }
                    else if (_partner.Arrived && !there)
                    {
                        _partner.WalkTo(_theirs);   // stopped short: on again
                    }
                    // Beside them at the front, on either side: near enough, and not on top of them.
                    if (there && CitySpots.Flat(player - _front) < 1.4f * 1.4f && CitySpots.Flat(player - _partner.Position) > 0.5f * 0.5f)
                    {
                        _d.Body.Bind(PlayerHelper.PlayerController);
                        _d.Body.Hold();
                        FacePartner(player);
                        _d.Cast.Face(_partner, player);
                        // The ring clips are made for a pair facing RingGap apart: the partner takes that step during the welcome.
                        _closing = Mathf.Abs(Mathf.Sqrt(CitySpots.Flat(player - _partner.Position)) - RingGap) > RingSlack * 0.5f
                                   && _partner.StepTo(player, RingGap);
                        _closeFrom = now;
                        _officiant.Work("talk_a", _d.Cast.Props, 0.3f);
                        FamilyText.Toast(NotificationType.Info, "reallife_family_officiant_welcome", Args.Us, 7f);
                        _step = Step.Welcome;
                        _until = now + 5f;
                    }
                    break;
                case Step.Welcome:
                    if (_closing && ((now - _closeFrom > 0.4f && _partner.Arrived && _partner.Speed < 0.05f) || now >= _until))
                    {
                        _closing = false;
                        _partner.Stand();
                        _d.Cast.Face(_partner, player);
                    }
                    if (now >= _until)
                    {
                        _officiant.Idle(0.3f);
                        _vow = 0;
                        _d.Ask("reallife_family_vow_header", "reallife_family_vow_body", Args.Us, "reallife_family_vow_heart", "reallife_family_vow_funny", () => _vow = 1, () => _vow = 2);
                        _step = Step.Vows;
                        _until = now + 60f;
                    }
                    break;
                case Step.Vows:
                    if (_vow == 0 && now < _until)
                    {
                        if (_d.HasQuestions || HudConfirm.isOpen)
                        {
                            _quietSince = 0f;
                        }
                        else if (_quietSince <= 0f)
                        {
                            _quietSince = now;
                        }
                        else if (now - _quietSince > 2.5f)
                        {
                            _vow = 1;   // the box was closed without an answer: from the heart it is
                        }
                        break;
                    }
                    if (_vow == 0)
                    {
                        _vow = 1;
                    }
                    if (!_d.Body.Clip("talk_b", 0.3f))
                    {
                        _d.Body.Rest();
                    }
                    FamilyText.Toast(NotificationType.Info, _vow == 1 ? FamilyText.Pick(HeartVows, ref _lastHeart) : FamilyText.Pick(FunnyVows, ref _lastFunny), Args.Us, 8f);
                    _step = Step.TheirVow;
                    _until = now + 6f;
                    break;
                case Step.TheirVow:
                    if (now >= _until)
                    {
                        _d.Body.Rest(0.3f);
                        _partner.Work("talk_a", _d.Cast.Props, 0.3f);
                        FamilyText.Toast(NotificationType.Info, FamilyText.Pick(TheirVows, ref _lastTheirs), Args.Us, 8f);
                        _step = Step.Rings;
                        _until = now + 6f;
                    }
                    break;
                case Step.Rings:
                    if (now >= _until)
                    {
                        _partner.Idle(0.3f);
                        // Both halves start together (the ring goes on at 1.86 s in each), and only at the distance the pair
                        // was made for; otherwise the player's half plays alone.
                        bool paired = Mathf.Abs(Mathf.Sqrt(CitySpots.Flat(player - _partner.Position)) - RingGap) <= RingSlack
                                      && ClipLibrary.Has("ring_present") && ClipLibrary.Has("ring_receive");
                        if (!_d.Body.Clip("ring_present", 0.3f))
                        {
                            _d.Body.Clip("job_hand_over", 0.3f);   // something small held out: the bundle has no ring clip
                        }
                        else if (paired)
                        {
                            _partner.Work("ring_receive", _d.Cast.Props, 0.3f);
                        }
                        FamilyText.Toast(NotificationType.Info, "reallife_family_rings_exchanged", Args.Us.With(FamilyText.T(Courtship.RingName(FamilyState.S.Ring))), 6f);
                        _step = Step.Pronounce;
                        _until = now + 3.5f;
                    }
                    break;
                case Step.Pronounce:
                    if (now >= _until)
                    {
                        _d.Body.Rest(0.3f);
                        _officiant.Work("talk_a", _d.Cast.Props, 0.3f);
                        FamilyText.Toast(NotificationType.Success, "reallife_family_officiant_pronounce", Args.Us, 7f);
                        _step = Step.Applause;
                        _until = now + 3.5f;
                    }
                    break;
                case Step.Applause:
                    if (now >= _until)
                    {
                        _married = true;
                        _officiant.Work("clap", _d.Cast.Props, 0.25f);
                        if (_guest != null && !_guest.Lost)
                        {
                            _guest.Work("clap", _d.Cast.Props, 0.25f);
                        }
                        for (int i = 0; i < _borrowed.Count; i++)
                        {
                            if (!_borrowed[i].Lost)
                            {
                                // Every other guest cheers, the rest clap, each a little after the last so that nobody moves in step.
                                bool cheers = i % 2 == 1 && ClipLibrary.Has("cheer_loop") && _borrowed[i].Work("cheer_loop", _d.Cast.Props, 0.25f, ClipEnd.Return, true, i * 0.37f);
                                if (!cheers)
                                {
                                    _borrowed[i].Work("clap", _d.Cast.Props, 0.25f, ClipEnd.Return, true, i * 0.3f);
                                }
                            }
                        }
                        if (!_partner.Work("cheer", _d.Cast.Props, 0.3f))
                        {
                            _partner.Work("wave_hello", _d.Cast.Props, 0.3f);
                        }
                        FamilyModifiers.Add(FamilyModifiers.Joy);
                        _step = Step.Photo;
                        _until = now + 2.2f;
                    }
                    break;
                case Step.Photo:
                    if (now >= _until && !_d.Busy)
                    {
                        _d.Body.Free();
                        _d.Run(_d.Photograph("wedding"));
                        _step = Step.Leaving;
                        _until = now + 2.5f;
                    }
                    break;
                case Step.Dance:
                    Dance(now, player);
                    break;
                case Step.Leaving:
                    if (now >= _until && !_d.Busy)
                    {
                        if (!_danced && BeginDance(now, player))
                        {
                            break;   // the couple's first dance, then the cut to the reception
                        }
                        _step = Step.Cut;
                        if (!_d.Run(_d.Passage("reallife_family_passage_reception", 4, () => { Release(); WeddingDay.Feast(); }, () => WeddingDay.Complete(true, Free))))
                        {
                            Release();
                            WeddingDay.Complete(true, Free);
                            Done = true;
                        }
                    }
                    break;
            }
        }

        // The first dance, after the photograph: the partner steps in to the clips' distance. False, with nothing changed, without the clips.
        private bool BeginDance(float now, Vector3 player)
        {
            _danced = true;
            if (!ClipLibrary.Has(LeadClip) || !ClipLibrary.Has(FollowClip))
            {
                return false;
            }
            _d.Body.Bind(PlayerHelper.PlayerController);
            _d.Body.Hold();
            FacePartner(player);
            _d.Cast.Face(_partner, player);
            _closing = Mathf.Abs(Mathf.Sqrt(CitySpots.Flat(player - _partner.Position)) - DanceGap) > DanceSlack * 0.5f && _partner.StepTo(player, DanceGap);
            _closeFrom = now;
            _dancing = false;
            _step = Step.Dance;
            _until = now + 4f;   // the longest the step in may take
            return true;
        }

        private void FacePartner(Vector3 player)
        {
            Vector3 to = _partner.Position - player;
            to.y = 0f;
            if (to.sqrMagnitude > 0.04f)
            {
                PlayerHelper.PlayerController.Character.transform.rotation = Quaternion.LookRotation(to);
            }
        }

        // Stepping in: once the partner stops, the pair plays only at the gap the clips were made for. Dancing: one turn, then both are let go.
        private void Dance(float now, Vector3 player)
        {
            if (!_dancing)
            {
                if (!(now >= _until || (now - _closeFrom > 0.4f && _partner.Arrived && _partner.Speed < 0.05f)))
                {
                    return;
                }
                _partner.Stand();
                _d.Cast.Face(_partner, player);
                FacePartner(player);
                float gap = Mathf.Sqrt(CitySpots.Flat(player - _partner.Position));
                bool fits = Mathf.Abs(gap - DanceGap) <= DanceSlack;
                if (fits && _d.Body.Clip(LeadClip, 0.4f))
                {
                    if (_partner.Work(FollowClip, _d.Cast.Props, 0.4f))
                    {
                        ClipLibrary.Played(LeadClip, "the player");
                        _dancing = true;
                        _until = now + DanceSeconds;
                        FamilyText.Toast(NotificationType.Info, "reallife_family_first_dance", Args.Us, 7f);
                    }
                    else
                    {
                        _d.Body.Rest(0.3f);   // the pair is both or neither
                    }
                }
                if (Meta.Dev.Verbose)
                {
                    FamilyDirector.Logger?.Info("Family: the first dance, " + gap.ToString("F2", CultureInfo.InvariantCulture) + " m apart (made for "
                                                + DanceGap.ToString("F2", CultureInfo.InvariantCulture) + " m, give or take "
                                                + DanceSlack.ToString("F2", CultureInfo.InvariantCulture) + "): " + (_dancing ? "played." : "not played."));
                }
                if (!_dancing)
                {
                    _d.Body.Free();
                    _step = Step.Leaving;
                    _until = now;
                }
                return;
            }
            if (now >= _until)
            {
                _d.Body.Rest(0.4f);
                _partner.Idle(0.4f);
                _d.Body.Free();
                _dancing = false;
                _step = Step.Leaving;
                _until = now;
            }
        }

        // The player left, or somebody was lost: everybody goes under a quick cut, and the day can be begun again while it lasts.
        private void Abandon(string toast)
        {
            _d.Body.Release();
            FamilyText.Toast(NotificationType.Warning, toast, Args.Us, 9f);
            _step = Step.Cut;
            if (_married)
            {
                // Vows said, rings on: married all the same, with the party skipped.
                if (!_d.Run(_d.Passage("reallife_family_passage_reception", 0, Release, () => WeddingDay.Complete(true, Free))))
                {
                    Release();
                    WeddingDay.Complete(true, Free);
                    Done = true;
                }
                return;
            }
            if (!_d.Run(_d.Passage("reallife_family_passage_gathering", 0, Release, null)))
            {
                Release();
                Done = true;
            }
        }

        private void Release()
        {
            if (_released)
            {
                return;
            }
            _released = true;
            _d.Cast.Release(_partner);
            _d.Cast.Release(_officiant);
            _d.Cast.Release(_guest);
            _partner = _officiant = _guest = null;
            for (int i = 0; i < _borrowed.Count; i++)
            {
                if (_city != null)
                {
                    _city.Release(_borrowed[i]);
                }
            }
            _borrowed.Clear();
            for (int i = 0; i < _dressing.Count; i++)
            {
                _d.Props.Return(_dressing[i]);
            }
            _dressing.Clear();
        }

        public override void End()
        {
            if (_d == null)
            {
                return;
            }
            _d.Body.Release();
            Release();
        }
    }
}
