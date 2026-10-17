using Dancing;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>The dance floor of a nightclub: the game's own dances, with the key pressed on the beat.</summary>
    public sealed class DanceFloor : LeisureActivity
    {
        private const int Beats = 24;
        private const float Beat = 0.86f;   // seconds

        private Sweep _sweep;
        private int _beat = -1;        // the cursor's pass being played
        private bool _pressed;
        private int _hits, _sweet, _run, _bestRun, _points;
        private int _dance;
        private bool _over;

        public override LeisureKind Kind => LeisureKind.Dance;
        public override string TitleKey => "reallife_leisure_dance_title";

        public override bool Begin(float now, out string refusalKey)
        {
            refusalKey = null;
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (!BuildingManager.IsInsideBuilding || LeisureVenues.BusinessIn(manager) != LeisureVenues.Nightclub)
            {
                refusalKey = "reallife_leisure_dance_no_club";
                return false;
            }
            LeisureVenues.ScanIndoors(manager);
            if (LeisureVenues.DanceFloors.Count > 0 && LeisureVenues.Nearest(LeisureVenues.DanceFloors, Ctx.Position, 4.5f) == null)
            {
                refusalKey = "reallife_leisure_dance_no_floor";
                return false;
            }
            if (Helpers.PlayerHelper.IsHoldingItem || !Ctx.Body.Usable)
            {
                refusalKey = "reallife_leisure_hands_full";
                return false;
            }
            Ctx.Body.Hold();
            _dance = Random.Range(0, 9);
            if (!Ctx.Body.Dance((DanceType)_dance))
            {
                refusalKey = "reallife_leisure_no_clips";
                return false;
            }
            Anchor = Ctx.Position;
            Leash = 3f;
            _sweep.Begin(now + 1.5f, Beat, 0.5f, 0.24f, 0.08f);   // a bar to find the beat first
            Meter = true;
            ZoneCentre = _sweep.Centre;
            ZoneWidth = _sweep.Width;
            SweetWidth = _sweep.Sweet;
            MeterLabel = T("reallife_leisure_dance_meter");
            Prompt = T("reallife_leisure_dance_prompt");
            Show(T("reallife_leisure_dance_go"), T("reallife_leisure_dance_status", "run", "0", "points", "0"));
            return true;
        }

        public override void Frame(float now, float delta)
        {
            if (_over || now < _sweep.Start)
            {
                MeterCursor = 0f;
                return;
            }
            MeterCursor = _sweep.At(now);
            int pass = (int)((now - _sweep.Start) / Beat);
            if (pass != _beat)
            {
                if (_beat >= 0 && !_pressed)
                {
                    Miss();
                }
                _beat = pass;
                _pressed = false;
                if (_beat >= Beats)
                {
                    Conclude();
                }
            }
        }

        public override void Interact(float now)
        {
            if (_over || now < _sweep.Start || _pressed || _beat < 0)
            {
                return;
            }
            _pressed = true;
            int judged = _sweep.Judge(now);
            if (judged == 0)
            {
                Miss();
                return;
            }
            _hits++;
            _run++;
            _bestRun = Mathf.Max(_bestRun, _run);
            _points += judged == 2 ? 3 : 1;
            if (judged == 2)
            {
                _sweet++;
            }
            LeisureSound.Cue(judged == 2 ? LeisureCue.Good : LeisureCue.Tick);
            if (_run % 6 == 0)
            {
                _dance = (_dance + 1 + Random.Range(0, 7)) % 9;   // a different one
                Ctx.Body.Dance((DanceType)_dance);
                Objective = T("reallife_leisure_dance_new_move");
            }
            Status = T("reallife_leisure_dance_status", "run", _run.ToString(), "points", _points.ToString());
        }

        private void Miss()
        {
            if (_run > 0)
            {
                _run = 0;
                Objective = T("reallife_leisure_dance_lost_beat");
                Status = T("reallife_leisure_dance_status", "run", "0", "points", _points.ToString());
            }
        }

        public override void Tick(float now)
        {
        }

        private void Conclude()
        {
            _over = true;
            Meter = false;
            Prompt = null;
            Score = _points + _bestRun * 2;
            CardTitle = T("reallife_leisure_dance_title");
            CardBody = T("reallife_leisure_dance_result", "hits", _hits.ToString(), "beats", Beats.ToString())
                       + "\n" + T("reallife_leisure_dance_result_run", "run", _bestRun.ToString(), "points", Score.ToString());
            Mood = _hits >= 18 ? ModState.MoodUpBig : _hits >= 12 ? ModState.MoodUp : null;
            LeisureMoney.Tire(3f);
            Finish(true);
        }

        public override void End()
        {
        }
    }

    /// <summary>A drink at a nightclub's bar: at the bar where it fits drink_bar, else where they stand.</summary>
    public sealed class BarDrinks : LeisureActivity
    {
        public const float TipsyFrom = 2f;
        public const float ServedUpTo = 4f;

        private static readonly int[] DrinkPrices = { 8, 14, 4 };   // beer, cocktail, soft drink, at the start's prices

        internal static int DrinkPrice(int drink) => Prices.At(DrinkPrices[drink]);
        private static readonly float[] Units = { 0.8f, 1.2f, 0f };
        private static readonly string[] NameKeys = { "reallife_leisure_drink_beer", "reallife_leisure_drink_cocktail", "reallife_leisure_drink_soft" };

        // drink_bar (CityAssets): a bar top 1.05 m up, the glass 0.35 m ahead and 0.06 m right of the drinker, whose
        // toes are 0.25 m from the bar's edge.
        private const string BarClip = "drink_bar";
        private const string GlassProp = "drink_glass";
        private const float BarTop = 1.05f, BarTopSlack = 0.05f, BarEdge = 0.25f, GlassAhead = 0.35f, GlassRight = 0.06f;
        private const float StepUpWithin = 0.5f, StepUpSeconds = 9f;   // metres; seconds

        private enum Phase { Choosing, StepUp, Settle, Drinking }

        private float _doneAt = -1f;
        private int _ordered = -1;
        private Phase _phase;
        private ItemController _bar;
        private Transform _glass;           // ours on the bar; the body's while in the hand
        private Vector3 _stand, _facing, _onBar;
        private float _stepUpBy, _gripAt = -1f, _releaseAt = -1f;

        public override LeisureKind Kind => LeisureKind.Drinks;
        public override string TitleKey => "reallife_leisure_drinks_title";

        public override bool Begin(float now, out string refusalKey)
        {
            refusalKey = null;
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (!BuildingManager.IsInsideBuilding || LeisureVenues.BusinessIn(manager) != LeisureVenues.Nightclub)
            {
                refusalKey = "reallife_leisure_dance_no_club";
                return false;
            }
            LeisureVenues.ScanIndoors(manager);
            ItemController bar = LeisureVenues.Nearest(LeisureVenues.Bars, Ctx.Position, 3.2f);
            if (bar == null)
            {
                refusalKey = "reallife_leisure_drinks_no_bar";
                return false;
            }
            if (Helpers.PlayerHelper.IsHoldingItem || !Ctx.Body.Usable)
            {
                refusalKey = "reallife_leisure_hands_full";
                return false;
            }
            _bar = bar;
            Anchor = bar.transform.position;
            Leash = 4.5f;
            var labels = new string[3];
            for (int i = 0; i < 3; i++)
            {
                labels[i] = T("reallife_leisure_drinks_choice", "drink", T(NameKeys[i]), "price", JobText.Money(DrinkPrice(i)));
            }
            Choices = labels;
            Show(T("reallife_leisure_drinks_ask"));
            return true;
        }

        public override void Choose(int choice, float now)
        {
            if (_ordered >= 0 || choice < 0 || choice > 2)
            {
                return;
            }
            float had = LeisureState.DrinksAt(TimeHelper.NowInMinutes());
            if (Units[choice] > 0f && had >= ServedUpTo)
            {
                Objective = T("reallife_leisure_drinks_enough");
                LeisureSound.Cue(LeisureCue.Bad);
                return;
            }
            if (!LeisureMoney.Charge(DrinkPrice(choice), "reallife_transaction_drinks"))
            {
                Objective = T("reallife_leisure_no_money");
                LeisureSound.Cue(LeisureCue.Bad);
                return;
            }
            _ordered = choice;
            Choices = null;
            Objective = T("reallife_leisure_drinks_served", "drink", T(NameKeys[choice]));
            LeisureSound.Cue(LeisureCue.Tick);
            if (Serve())
            {
                // The player has a few seconds to step up to the marked place, or drinks where they stand.
                _phase = Phase.StepUp;
                _stepUpBy = now + StepUpSeconds;
                Marked = true;
                MarkAt = _stand;
                MarkLabel = T(NameKeys[choice]);
                Tick(now);
                return;
            }
            Sip(now);
        }

        // Wherever drink_bar does not fit: the sip clip with the glass in the hand throughout, or no clip at all.
        private void Sip(float now)
        {
            Marked = false;
            if (_glass != null)
            {
                Ctx.Props.Return(_glass);
                _glass = null;
            }
            Ctx.Body.Hold();
            Ctx.Body.Face(Anchor);
            bool sipping = Ctx.Body.Play("sip", GlassProp, 0.3f);
            _phase = Phase.Drinking;
            _gripAt = _releaseAt = -1f;
            _doneAt = now + (sipping ? 3.6f : 1.6f);
        }

        /// <summary>Stands the glass on the bar if it fits drink_bar: square to the player, top 1.05 m up.</summary>
        private bool Serve()
        {
            if (_bar == null || !ClipLibrary.Has(BarClip) || !LeisureProps.Has(GlassProp))
            {
                return false;
            }
            Vector3 feet = Ctx.Position;
            Vector3 to = Anchor - feet;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f)
            {
                return false;
            }
            to.Normalize();
            const int layers = Physics.DefaultRaycastLayers;
            if (!Physics.Raycast(feet + Vector3.up * (BarTop - 0.03f), to, out RaycastHit face, 3.4f, layers, QueryTriggerInteraction.Ignore) || !OfBar(face.collider))
            {
                return false;
            }
            Vector3 facing = -face.normal;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.5f)
            {
                return false;
            }
            facing.Normalize();
            if (Vector3.Dot(facing, to) < 0.5f)
            {
                return false;   // seen from round the end of it
            }
            Vector3 stand = new Vector3(face.point.x, feet.y, face.point.z) - facing * BarEdge;
            Vector3 spot = stand + Vector3.Cross(Vector3.up, facing) * GlassRight + facing * GlassAhead;
            if (!Physics.Raycast(spot + Vector3.up * 1.6f, Vector3.down, out RaycastHit top, 1.2f, layers, QueryTriggerInteraction.Ignore) || !OfBar(top.collider)
                || top.normal.y < 0.96f || Mathf.Abs(top.point.y - feet.y - BarTop) > BarTopSlack
                || !NavMesh.SamplePosition(stand, out NavMeshHit floor, 0.25f, NavMesh.AllAreas) || Mathf.Abs(floor.position.y - feet.y) > 0.05f)
            {
                return false;
            }
            Transform glass = Ctx.Props.Take(GlassProp);
            if (glass == null)
            {
                return false;
            }
            _stand = stand;
            _facing = facing;
            _onBar = top.point;
            _glass = glass;
            StandGlass();
            return true;
        }

        private bool OfBar(Collider hit)
        {
            return hit != null && ReferenceEquals(hit.GetComponentInParent<ItemController>(), _bar);
        }

        private void StandGlass()
        {
            _glass.SetPositionAndRotation(_onBar, Quaternion.LookRotation(_facing));
            _glass.localScale = Vector3.one;
        }

        // drink_bar's grip and release marks: two comparisons a frame while a drink is drunk that way.
        public override void Frame(float now, float delta)
        {
            if (_gripAt >= 0f && now >= _gripAt)
            {
                _gripAt = -1f;
                if (!Ctx.Body.PickUp(BarClip, _glass))
                {
                    _releaseAt = -1f;   // it stays on the bar
                }
            }
            if (_releaseAt >= 0f && now >= _releaseAt)
            {
                _releaseAt = -1f;
                if (ReferenceEquals(Ctx.Body.SetDown(), _glass) && _glass != null)
                {
                    StandGlass();
                }
            }
        }

        public override void Tick(float now)
        {
            if (_phase == Phase.StepUp)
            {
                if (CitySpots.Flat(_stand - Ctx.Position) < StepUpWithin * StepUpWithin)
                {
                    Marked = false;
                    Ctx.Body.Hold();
                    if (!Ctx.Body.Settle(_stand))
                    {
                        Sip(now);
                        return;
                    }
                    Ctx.Body.Face(_stand + _facing * 2f);
                    _phase = Phase.Settle;
                    _doneAt = now + 0.45f;
                }
                else if (now >= _stepUpBy)
                {
                    Sip(now);
                }
                return;
            }
            if (_phase == Phase.Settle)
            {
                if (now < _doneAt || Ctx.Body.NeedsFrame)
                {
                    return;
                }
                if (!ClipLibrary.TryGet(BarClip, out CityClip clip) || !Ctx.Body.PlayBare(BarClip, 0.3f))
                {
                    Sip(now);
                    return;
                }
                _phase = Phase.Drinking;
                _gripAt = now + clip.Mark("grip", 0.5f);
                _releaseAt = now + clip.Mark("release", 3.0f);
                _doneAt = now + clip.Length + 0.3f;
                return;
            }
            if (_ordered < 0 || now < _doneAt)
            {
                return;
            }
            float minutes = TimeHelper.NowInMinutes();
            if (Units[_ordered] > 0f)
            {
                LeisureState.AddDrink(minutes, Units[_ordered]);
            }
            float had = LeisureState.DrinksAt(minutes);
            if (had >= TipsyFrom)
            {
                ModState.AddModifier(ModState.Tipsy);
                JobText.Toast(UI.Notification.NotificationType.Info, "reallife_leisure_drinks_tipsy", 7f);
            }
            else
            {
                Mood = ModState.MoodUp;
            }
            Score = 0;
            Finish(true);
        }

        public override void End()
        {
            // A glass still in the hand went back with the body's release already; returning it twice is safe.
            Marked = false;
            _gripAt = _releaseAt = -1f;
            if (_glass != null)
            {
                Ctx.Props.Return(_glass);
                _glass = null;
            }
            _bar = null;
        }
    }

    /// <summary>Taking in the view at a landmark: a selfie with it behind, and the place ticked off.</summary>
    public sealed class Sightseeing : LeisureActivity
    {
        private Landmark _mark;
        private float _doneAt;

        public override LeisureKind Kind => LeisureKind.Sightseeing;
        public override string TitleKey => "reallife_leisure_sights_title";

        public override bool Begin(float now, out string refusalKey)
        {
            refusalKey = null;
            LeisureVenues.FindLandmarks();
            _mark = LeisureVenues.LandmarkAt(Ctx.Position);
            if (_mark == null)
            {
                refusalKey = "reallife_leisure_sights_none_here";
                return false;
            }
            if (Helpers.PlayerHelper.IsHoldingItem || !Ctx.Body.Usable)
            {
                refusalKey = "reallife_leisure_hands_full";
                return false;
            }
            Anchor = Ctx.Position;
            Leash = 3f;
            Ctx.Body.Hold();
            bool selfie = Ctx.Body.Play(SightseeingRules.SelfieClip, null, 0.3f);
            bool photo = selfie || Ctx.Body.Play(SightseeingRules.PhotoClip, null, 0.3f);
            Vector3 at = Ctx.Position;
            if (selfie && SightseeingRules.BackTo(at.x, at.z, _mark.Position.x, _mark.Position.z, out float x, out float z))
            {
                Ctx.Body.Face(at + new Vector3(x, 0f, z));
            }
            _doneAt = now + SightseeingRules.Lasts(photo);
            Show(T("reallife_leisure_sights_looking", "place", T(_mark.NameKey)));
            return true;
        }

        public override void Tick(float now)
        {
            if (now < _doneAt)
            {
                return;
            }
            bool first = LeisureState.See(_mark.Id);
            int seen = LeisureState.SeenCount;
            int all = LeisureVenues.Landmarks.Count;
            Score = seen;
            CardTitle = T(_mark.NameKey);
            CardBody = T(first ? "reallife_leisure_sights_first" : "reallife_leisure_sights_again")
                       + "\n" + T("reallife_leisure_sights_count", "seen", seen.ToString(), "all", all.ToString())
                       + (first && seen >= all ? "\n" + T("reallife_leisure_sights_all") : string.Empty);
            Mood = first ? (seen >= all ? ModState.MoodUpBig : ModState.MoodUp) : null;
            if (first)
            {
                LeisureSound.Cue(LeisureCue.Good);
            }
            Finish(true);
        }

        public override void End()
        {
        }
    }
}
