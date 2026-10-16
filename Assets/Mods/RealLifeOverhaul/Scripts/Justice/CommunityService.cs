using System;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A community service shift, played: eight pieces of litter on the pavements round where
    /// the shift was begun, one at a time. A marker leads to the next; standing by it, the
    /// interaction key picks it up: the game walks the player the last step to the place the
    /// litter_pick clip is made for (the pile 0.55 m ahead and 0.05 m to the right of the middle,
    /// where its right hand closes), they are turned square to it and held while the clip plays,
    /// and the litter is gone at the clip's own "grip" moment. Without that clip it is the mod's
    /// kneeling clip from where they stand, as before. A whole shift is four of the hours owed;
    /// one given up part of the way counts half an hour a piece.
    ///
    /// Its own small loop, not the Jobs framework: one prop made once and moved from place to
    /// place, a spot asked of CitySpots (pavement, clear, reachable on foot) when a piece is laid,
    /// a tick five times a second while a shift is on and nothing at all otherwise. It ends, and
    /// puts everything back, when the hours are done, when the player goes indoors or drives off,
    /// when the police want them, after ten minutes, and on any fault.
    /// </summary>
    public sealed class CommunityService
    {
        private const float Reach = 1.0f;
        private const float PickSeconds = 1.8f;        // of a clip that does not say (litter_pick says: its length)
        private const float GoneAt = 1.1f;             // into the pick: the litter is in the bag (litter_pick says: its "grip" mark)
        private const string PickClip = "litter_pick";
        private const float PickAhead = 0.55f;         // metres ahead of the feet at which litter_pick's right hand closes on the ground (CityAssets/README.md)
        private const float PickRight = 0.05f;         // ...and to the right of the middle
        private const float StandSlack = 0.10f;        // nearer the place it is made for than this is there: every piece of the pile lies within 0.23 m of its middle
        private const float StepSeconds = 2.5f;        // given to the game to walk them that last step
        private const int StepTries = 2;               // steps that never arrived after which a shift stops asking for them
        private const float ShiftMostSeconds = 600f;
        private const float TooFar = 150f;
        private const NavigationBlocker Blocker = NavigationBlocker.JobOfferPanel;

        private static readonly string[] Clips = { PickClip, "kneel_work" };   // the first the bundle has

        private readonly IModLogger _logger;
        private GameObject _litter;
        private Vector3 _at;
        private int _picked;
        private float _startedAt, _pickEnds, _goneAt;
        private bool _picking, _inReach;
        private bool _stepping;        // the key was pressed and the game is walking them to where the clip is made for
        private Vector3 _stand;        // ...which is here
        private float _stepUntil;
        private int _stepFailures;     // this shift
        private bool _stepSaid;
        private PlayerController _held;
        private ActivityAnimator _animator;
        private string _label;
        private readonly DoorPanel _door;          // the game's own panel over the litter, with one button
        private readonly GuideMark _mark = new GuideMark("community service");
        private readonly Action _onPick;           // made once: the panel's button
        private readonly Func<string> _doorWhat;
        private ShiftCard _card;                   // a copy of the game's job panel: the pieces picked and the hours owed
        private bool _cardTried;
        private string _headline, _pieces;
        private int _owed = -1;

        public CommunityService(IModLogger logger)
        {
            _logger = logger;
            _door = new DoorPanel(logger, "Justice check (litter panel)");
            _onPick = _door.Press(PickPressed);
            _doorWhat = () => "litter";
        }

        /// <summary>The director whose life keeps this one's marker: destroyed or stopped by the guard, the marker goes (Core/GameUi/GuideMark.Keep).</summary>
        internal void KeptBy(Behaviour host)
        {
            _mark.Keep(host);
        }

        public bool Active { get; private set; }

        /// <summary>True while the panel's key would pick a piece up.</summary>
        public bool WantsKey => Active && (_door.TakesKey || (_inReach && !_picking && !_stepping));

        /// <summary>Begins a shift where the player stands. Says why not if it cannot.</summary>
        public JusticeRefusal Start(PlayerController player)
        {
            if (Active)
            {
                return JusticeRefusal.Busy;
            }
            if (player == null)
            {
                return JusticeRefusal.NotLoaded;
            }
            if (BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle)
            {
                return JusticeRefusal.Indoors;
            }
            _picked = 0;
            _picking = false;
            _inReach = false;
            _stepping = false;
            _stepFailures = 0;
            if (!Lay(player.transform.position))
            {
                return JusticeRefusal.NoPavement;
            }
            Active = true;
            _startedAt = Time.time;
            _label = JusticeText.T("reallife_justice_cs_marker");
            Status();
            JusticeText.Toast(NotificationType.Info, "reallife_justice_cs_started", "pieces", JusticeText.N(JusticeLaw.CommunityShiftPieces), 9f);
            _logger?.Info("Justice: community service shift begun (" + JusticeState.CommunityHours + " h owed).");
            return JusticeRefusal.None;
        }

        /// <summary>Five times a second while a shift is on: the shift itself, then what is on screen for it.</summary>
        public void Tick(float now, PlayerController player)
        {
            if (!Active)
            {
                return;
            }
            Run(now, player);
            if (Active && player != null)
            {
                Present(player);
            }
        }

        // The marker leads to the litter and the panel stands over it while it can be picked up; both are told only what changed
        // (a panel the game refused is tried again on the next tick).
        private void Present(PlayerController player)
        {
            if (_picking || _stepping)
            {
                _mark.Hide();
                _door.Hide("the litter is being picked up");
                return;
            }
            _mark.Show(_at, _label);
            if (!_inReach)
            {
                _door.Hide("out of reach of the litter");
                return;
            }
            WorldPanel panel = _door.Panel;
            panel.Lines(JusticeText.T("reallife_justice_door_litter"), null, null, WorldPanel.White());
            panel.Button(0, ButtonPick, _onPick, true, true);
            panel.Button(1, null, null, false, false);
            panel.Button(2, null, null, false, false);
            panel.Button(3, null, null, false, false);
            _door.Show(_at, player.transform.position, 1.2f, _doorWhat);
        }

        private const string ButtonPick = "reallife_justice_button_pick";

        // The panel's button: the key was pressed by the litter.
        private void PickPressed()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (!Active || player == null || !_inReach || _picking || _stepping || ThirdPersonCamera.IsUiOpen())
            {
                return;
            }
            try
            {
                Pick(player);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                Stop(false);
            }
        }

        private void Run(float now, PlayerController player)
        {
            if (player == null || BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle || PoliceAttention.IsBusy || JobDirector.OnShift
                || now - _startedAt > ShiftMostSeconds || TimeHelper.CurrentHour >= 22 || TimeHelper.CurrentHour < 6)
            {
                Stop(false);
                return;
            }
            Vector3 offset = _at - player.transform.position;
            float flat = offset.x * offset.x + offset.z * offset.z;
            if (flat > TooFar * TooFar)
            {
                Stop(false);
                return;
            }
            if (_stepping)
            {
                Step(now, player, flat);
                return;
            }
            if (!_picking)
            {
                _inReach = flat < Reach * Reach && Mathf.Abs(offset.y) < 2f;
                return;
            }
            if (now >= _goneAt && _litter != null && _litter.activeSelf)
            {
                _litter.SetActive(false);
            }
            if (now < _pickEnds)
            {
                return;
            }
            // One more in the bag.
            _picking = false;
            Free();
            _picked++;
            var save = SaveGameManager.Current;
            if (save != null && save.Energy > 15f)
            {
                EnergyHelper.SpentEnergyOnce(1.2f);
            }
            if (_picked >= JusticeLaw.CommunityShiftPieces || !Lay(player.transform.position))
            {
                Stop(true);
                return;
            }
            Status();
        }

        // The key was pressed by the litter. With the clip made for it they are first walked, by the game itself, to
        // the place that clip is measured from; anywhere near enough already, or with no such clip, it begins at once.
        private void Pick(PlayerController player)
        {
            _inReach = false;
            try
            {
                Vector3 here = player.transform.position;
                Vector3 to = _at - here;
                to.y = 0f;
                Vector3 facing = to;
                if (facing.sqrMagnitude < 0.15f * 0.15f)
                {
                    facing = player.transform.forward;   // standing on it: back along the way they face
                    facing.y = 0f;
                }
                facing = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.forward;
                _stand = _at - facing * PickAhead - Vector3.Cross(Vector3.up, facing) * PickRight;
                _stand.y = here.y;
                Vector3 gap = _stand - here;
                gap.y = 0f;
                if (gap.sqrMagnitude > StandSlack * StandSlack && _stepFailures < StepTries && ClipLibrary.Has(PickClip) && ActivityAnimators.Working)
                {
                    _stepping = true;
                    _stepUntil = Time.time + StepSeconds;
                    player.SetGoal(_stand, null);
                    return;
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                _stepping = false;
            }
            Bend(player);
        }

        // Five times a second while they are walked that step: there, and down to it; steered away or never there, and
        // the prompt is back (a shift whose steps do not arrive stops asking for them and picks from where they stand).
        private void Step(float now, PlayerController player, float flatToLitter)
        {
            Vector3 gap = _stand - player.transform.position;
            gap.y = 0f;
            if (gap.sqrMagnitude <= StandSlack * StandSlack)
            {
                _stepping = false;
                Bend(player);
                return;
            }
            if (now < _stepUntil && flatToLitter < (Reach + 1f) * (Reach + 1f))
            {
                return;
            }
            _stepping = false;
            _stepFailures++;
            if (!_stepSaid)
            {
                _stepSaid = true;
                _logger?.Info($"Justice: the player was not walked to the place litter_pick is made for ({gap.magnitude:F2} m short after {StepSeconds:F1} s, "
                              + $"allowed {StandSlack:F2}); after {StepTries} such in a shift the litter is picked from where they stand. (Said once.)");
            }
        }

        // Held, turned so that the litter is where the clip's hand closes, and down to it: only with a clip made for the
        // game's skeleton, never a pose set by hand.
        private void Bend(PlayerController player)
        {
            _picking = true;
            _inReach = false;
            float now = Time.time;
            _pickEnds = now + PickSeconds;
            _goneAt = now + GoneAt;
            try
            {
                player.ResetNavigation();
                player.SetNavigationBlocker(Blocker);
                _held = player;
                Vector3 to = _at - player.transform.position;
                to.y = 0f;
                float reach = to.magnitude;
                if (reach > 0.1f)
                {
                    // (The pile a little to the right of the way they face, by the hand's 5 cm.)
                    Quaternion square = Quaternion.LookRotation(to / reach, Vector3.up)
                                        * Quaternion.Euler(0f, -Mathf.Asin(Mathf.Clamp01(PickRight / reach)) * Mathf.Rad2Deg, 0f);
                    player.transform.rotation = square;
                    if (player.Character != null)
                    {
                        player.Character.ForceToRotation(square);   // (as the home routines turn them to their furniture)
                    }
                }
                ThirdPersonCharacter body = player.Character;
                if (body != null && body.animator != null && body.visible && !RagdollSystem.Holds(body) && ActivityAnimators.Working && ClipLibrary.Ready)
                {
                    for (int i = 0; i < Clips.Length; i++)
                    {
                        if (!ClipLibrary.TryGet(Clips[i], out CityClip clip))
                        {
                            continue;
                        }
                        ActivityAnimator animator = ActivityAnimators.For(body.animator);
                        if (animator != null && animator.Usable && animator.Play(clip.Clip, 0.25f))
                        {
                            _animator = animator;
                            if (!clip.Loop)
                            {
                                // The clip's own moments: the litter goes as the fingers close on it, and they are let go as it ends.
                                _pickEnds = now + clip.Length;
                                _goneAt = now + Mathf.Clamp(clip.Mark("grip", GoneAt), 0.1f, clip.Length);
                            }
                        }
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // Lets the player go and stands them up again. Safe when they were not held.
        private void Free()
        {
            try
            {
                if (_animator != null)
                {
                    _animator.Stop(0.25f);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _animator = null;
            PlayerController player = _held;
            _held = null;
            if (player != null)
            {
                try
                {
                    player.UnsetNavigationBlocker(Blocker);
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
            }
        }

        /// <summary>Ends the shift on any path: the hours worked are credited, the litter, the marker and the hold are gone.</summary>
        public void Stop(bool whole)
        {
            if (!Active)
            {
                return;
            }
            Active = false;
            _picking = false;
            _inReach = false;
            _stepping = false;
            Free();
            if (_litter != null)
            {
                _litter.SetActive(false);
            }
            _mark.Hide();
            _door.Hide("the shift is over");
            _card?.Show(false);
            int hours = whole && _picked >= JusticeLaw.CommunityShiftPieces ? JusticeLaw.CommunityShiftHours
                : _picked * JusticeLaw.CommunityShiftHours / JusticeLaw.CommunityShiftPieces;   // half an hour a piece, whole hours only
            hours = Mathf.Min(hours, JusticeState.CommunityHours);
            JusticeState.CommunityHours -= hours;
            _logger?.Info("Justice: community service shift over, " + _picked + " piece(s), " + hours + " h credited, " + JusticeState.CommunityHours + " h still owed.");
            if (SaveGameManager.Current == null)
            {
                return;
            }
            if (JusticeState.CommunityHours <= 0)
            {
                JusticeState.CommunityHours = 0;
                JusticeState.CommunityDue = 0;
                ModState.AddModifier(ModState.MoodUp);
                JusticeText.Toast(NotificationType.Success, "reallife_justice_cs_all_done", 10f);
            }
            else if (hours > 0)
            {
                JusticeText.Toast(NotificationType.Success, "reallife_justice_cs_shift_done", "hours", JusticeText.N(hours), "left", JusticeText.N(JusticeState.CommunityHours), 10f);
            }
            else
            {
                JusticeText.Toast(NotificationType.Warning, "reallife_justice_cs_nothing", 9f);
            }
        }

        public void Dispose()
        {
            Stop(false);
            _card?.Destroy();
            _card = null;
            if (_litter != null)
            {
                UnityEngine.Object.Destroy(_litter);
                _litter = null;
            }
        }

        // The shift's figures on the copy of the game's job panel: the pieces picked and the hours still owed. Without that copy
        // the pieces picked are sent as a notification each time, as the line used to be shown.
        private void Status()
        {
            string pieces = JusticeText.T("reallife_justice_cs_status", "done", JusticeText.N(_picked), "pieces", JusticeText.N(JusticeLaw.CommunityShiftPieces));
            if (!_cardTried || (_card != null && !_card.Alive))
            {
                _cardTried = true;
                _card = ShiftCard.Make(_logger);
                if (Meta.Dev.Verbose)
                {
                    _logger?.Info("Justice check (community card): " + (_card != null ? "a copy of the game's job panel is ready: PASS" : "the game's job panel could not be copied; the pieces are sent as notifications: FAIL") + ".");
                }
            }
            if (_card == null)
            {
                JusticeText.Toast(NotificationType.Info, "reallife_justice_raw", "text", pieces, 4f);
                return;
            }
            if (_headline == null)
            {
                _headline = JusticeText.T("reallife_justice_cs_card_title");
            }
            if (_owed != JusticeState.CommunityHours)
            {
                _owed = JusticeState.CommunityHours;
                _pieces = JusticeText.T("reallife_justice_cs_card_owed", "hours", JusticeText.N(_owed));
            }
            _card.Title(_headline);
            _card.Line(0, pieces, WorldPanel.White());
            _card.Line(1, _pieces, WorldPanel.White());
            for (int i = 2; i < ShiftCard.Lines; i++)
            {
                _card.Line(i, null, WorldPanel.White());
            }
            _card.Show(true);
        }

        // The next piece: on pavement between 9 and 36 m away, clear of everything, somewhere the player can walk to.
        private bool Lay(Vector3 from)
        {
            for (int attempt = 0; attempt < 14; attempt++)
            {
                if (!CitySpots.PavementRing(from, 9f, 36f, false, out Vector3 spot) || !CitySpots.Stand(spot, 0.5f, out spot) || !CitySpots.Reachable(from, spot))
                {
                    continue;
                }
                if (_litter == null)
                {
                    _litter = Build();
                    if (_litter == null)
                    {
                        return false;
                    }
                }
                _at = spot;
                _litter.transform.SetPositionAndRotation(spot, Quaternion.Euler(0f, UnityEngine.Random.value * 360f, 0f));
                _litter.SetActive(true);
                return true;
            }
            return false;
        }

        // The modelled litter if the prop bundle has it; otherwise a crumpled sheet, a drinks can on its side and a flattened carton.
        private static GameObject Build()
        {
            if (!Shapes.Available)
            {
                return null;
            }
            var litter = new GameObject("RealLifeOverhaul Litter");
            Transform root = litter.transform;
            if (!Shapes.Prop("litter_pile", root))
            {
                Material paper = Shapes.Paint(new Color(0.86f, 0.85f, 0.80f));
                Material can = Shapes.Paint(new Color(0.72f, 0.14f, 0.12f));
                Material carton = Shapes.Paint(new Color(0.55f, 0.42f, 0.26f));
                if (paper == null || can == null || carton == null)
                {
                    UnityEngine.Object.Destroy(litter);
                    return null;
                }
                Transform ball = Shapes.Piece("Paper", root, Shapes.Cube, paper, new Vector3(0f, 0.05f, 0f), new Vector3(0.11f, 0.09f, 0.10f));
                ball.localRotation = Quaternion.Euler(18f, 35f, 12f);
                Transform tin = Shapes.Piece("Can", root, Shapes.Drum, can, new Vector3(0.16f, 0.033f, 0.05f), new Vector3(0.066f, 0.12f, 0.066f));
                tin.localRotation = Quaternion.Euler(90f, 0f, 40f);
                Transform flat = Shapes.Piece("Carton", root, Shapes.Cube, carton, new Vector3(-0.13f, 0.01f, 0.09f), new Vector3(0.19f, 0.02f, 0.13f));
                flat.localRotation = Quaternion.Euler(0f, 62f, 0f);
            }
            litter.SetActive(false);
            return litter;
        }
    }
}
