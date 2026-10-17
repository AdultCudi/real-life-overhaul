using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// A line in the water: five casts on one lot of bait. Cast, wait, strike inside the bite's window, then three
    /// timed pulls; the catch is weighed and sold. Without the rod clips an arm loop stands in.
    /// </summary>
    public sealed class Fishing : LeisureActivity
    {
        public static int Bait => Prices.At(6);
        private const int Casts = 5;

        private sealed class Fish
        {
            public readonly string Id, NameKey;
            public readonly int MinGrams, MaxGrams, Weight, ValuePerKilo;
            public readonly float Window, Zone;   // seconds to strike in; the fight zone's width

            public Fish(string id, string nameKey, int minGrams, int maxGrams, int weight, int valuePerKilo, float window, float zone)
            {
                Id = id;
                NameKey = nameKey;
                MinGrams = minGrams;
                MaxGrams = maxGrams;
                Weight = weight;
                ValuePerKilo = valuePerKilo;
                Window = window;
                Zone = zone;
            }
        }

        // What comes out of a city harbour, commonest first.
        private static readonly Fish[] Catch =
        {
            new Fish("perch", "reallife_leisure_fish_perch", 150, 450, 26, 8, 1.25f, 0.30f),
            new Fish("porgy", "reallife_leisure_fish_porgy", 250, 900, 22, 9, 1.15f, 0.28f),
            new Fish("boot", "reallife_leisure_fish_boot", 700, 900, 9, 0, 1.6f, 0.34f),
            new Fish("eel", "reallife_leisure_fish_eel", 300, 1400, 10, 10, 1.0f, 0.24f),
            new Fish("bluefish", "reallife_leisure_fish_bluefish", 800, 3200, 14, 9, 0.95f, 0.22f),
            new Fish("fluke", "reallife_leisure_fish_fluke", 700, 2800, 10, 14, 0.9f, 0.22f),
            new Fish("blackfish", "reallife_leisure_fish_blackfish", 900, 3800, 6, 13, 0.85f, 0.20f),
            new Fish("striped_bass", "reallife_leisure_fish_striped_bass", 2200, 9500, 3, 12, 0.75f, 0.17f)
        };

        private enum Step { Ready, Fly, Wait, Bite, Fight, Landed, Over }

        private const string RodProp = "fishing_rod";
        private const string WaitClip = "fish_wait", CastClip = "cast_rod", ReelClip = "reel_in", LandClip = "fish_land";
        private const float RodLength = 2.1f;     // metres, butt to tip ring along the prop's +Z
        private const float FlySeconds = 0.8f;

        private Step _step;
        private float _stepEnds;
        private Sweep _sweep;
        private Vector3 _water;
        private bool _surface;
        private Transform _float, _line;
        private float _floatDip;        // metres under, eased
        private float _flyFrom;
        private bool _rodClips;
        private string _rodClip;
        private int _cast;
        private int _castQuality;
        private int _scares;
        private Fish _fish;
        private int _grams;
        private int _pulls, _misses;
        private int _landed, _sold, _bestGrams;
        private bool _newKind;

        public override LeisureKind Kind => LeisureKind.Fishing;
        public override string TitleKey => "reallife_leisure_fishing_title";

        public override bool Begin(float now, out string refusalKey)
        {
            refusalKey = null;
            LeisureVenues.FindLandmarks();
            if (LeisureVenues.WatersideDistance(Ctx.Position) > LeisureReach.FishingLandmark || !LeisureVenues.WaterAhead(Ctx.Position, Ctx.Forward, out _water, out _surface))
            {
                refusalKey = "reallife_leisure_fishing_no_water";
                return false;
            }
            _rodClips = ClipLibrary.Has(WaitClip) && ClipLibrary.Has(CastClip) && ClipLibrary.Has(ReelClip) && ClipLibrary.Has(LandClip);
            _rodClip = null;
            if (!Ctx.Body.Usable || !(_rodClips || ClipLibrary.Has("umbrella_hold")) || !LeisureProps.Has(RodProp))
            {
                refusalKey = "reallife_leisure_no_clips";
                return false;
            }
            if (!LeisureMoney.CanPay(Bait))
            {
                refusalKey = "reallife_leisure_no_money";
                return false;
            }
            Ctx.Body.Hold();
            Ctx.Body.Face(_water);
            if (_rodClips && !Rod(WaitClip))
            {
                _rodClips = false;   // the stand-in pose, if that can be had
            }
            if ((!_rodClips && !Ctx.Body.Over("umbrella_hold", BodyMask.RightArm, RodProp)) || !LeisureMoney.Charge(Bait, "reallife_transaction_fishing_bait"))
            {
                refusalKey = "reallife_leisure_no_clips";
                return false;   // the director releases the body
            }
            if (_surface)
            {
                _float = Ctx.Props.Take("fishing_float");
                _line = Ctx.Props.Take("fishing_line");
                Stow();
            }
            Anchor = Ctx.Position;
            Leash = LeisureReach.StandingLeash;
            _cast = 0;
            Choices = new[] { null, null, T("reallife_leisure_fishing_pack_up") };
            ToReady(now);
            return true;
        }

        // The rod clips share the grip, so they cross-fade; a one-off holds its last pose, fish_wait's first.
        private bool Rod(string clip)
        {
            if (!_rodClips)
            {
                return false;
            }
            if (_rodClip == clip && Ctx.Body.ClipAlive)
            {
                return true;
            }
            if (!Ctx.Body.Play(clip, RodProp, 0.25f, ClipEnd.Hold))
            {
                return false;   // the pose stays as it was; the game goes on
            }
            _rodClip = clip;
            return true;
        }

        // Seconds from the start of a cast to the line leaving.
        private float CastRelease()
        {
            return _rodClips && ClipLibrary.TryGet(CastClip, out CityClip cast) ? Mathf.Max(0f, cast.Mark("release", 0.9f)) : 0f;
        }

        private void ToReady(float now)
        {
            _step = Step.Ready;
            _scares = 0;
            _sweep.Begin(now, 1.25f, 0.72f, 0.26f, 0.08f);
            Meter = true;
            ZoneCentre = _sweep.Centre;
            ZoneWidth = _sweep.Width;
            SweetWidth = _sweep.Sweet;
            MeterLabel = T("reallife_leisure_fishing_meter_cast");
            Prompt = T("reallife_leisure_fishing_prompt_cast");
            Status = T("reallife_leisure_fishing_status", "cast", (_cast + 1).ToString(), "caught", _landed.ToString());
            Objective = T("reallife_leisure_fishing_ready");
            Stow();
            Rod(WaitClip);
        }

        public override void Interact(float now)
        {
            switch (_step)
            {
                case Step.Ready:
                    _castQuality = _sweep.Judge(now);
                    Meter = false;
                    Prompt = null;
                    Objective = T(_castQuality == 2 ? "reallife_leisure_fishing_cast_great" : _castQuality == 1 ? "reallife_leisure_fishing_cast_good" : "reallife_leisure_fishing_cast_short");
                    LeisureSound.Cue(_castQuality > 0 ? LeisureCue.Tick : LeisureCue.Bad);
                    _step = Step.Fly;
                    _flyFrom = now + (Rod(CastClip) ? CastRelease() : 0f);
                    _stepEnds = _flyFrom + FlySeconds;
                    break;

                case Step.Wait:
                    // Struck at nothing: whatever was looking at the bait is gone.
                    _scares++;
                    LeisureSound.Cue(LeisureCue.Bad);
                    if (_scares >= 3)
                    {
                        Objective = T("reallife_leisure_fishing_scared_off");
                        NextCast(now, 2.5f);
                    }
                    else
                    {
                        Objective = T("reallife_leisure_fishing_too_soon");
                        _stepEnds = now + Random.Range(5f, 10f);
                    }
                    break;

                case Step.Bite:
                    _pulls = 0;
                    _misses = 0;
                    _step = Step.Fight;
                    _floatDip = 0.02f;
                    Objective = T("reallife_leisure_fishing_hooked");
                    Prompt = T("reallife_leisure_fishing_prompt_pull");
                    MeterLabel = T("reallife_leisure_fishing_meter_fight");
                    LeisureSound.Cue(LeisureCue.Good);
                    Rod(ReelClip);
                    Pull(now);
                    break;

                case Step.Fight:
                    if (_sweep.Judge(now) > 0)
                    {
                        _pulls++;
                        LeisureSound.Cue(LeisureCue.Tick);
                        if (_pulls >= 3)
                        {
                            Land(now);
                            return;
                        }
                    }
                    else
                    {
                        _misses++;
                        LeisureSound.Cue(LeisureCue.Bad);
                        if (_misses >= 2)
                        {
                            Meter = false;
                            Prompt = null;
                            Objective = T("reallife_leisure_fishing_got_off");
                            NextCast(now, 2.5f);
                            return;
                        }
                    }
                    Pull(now);
                    break;
            }
        }

        public override void Choose(int choice, float now)
        {
            if (choice == 2 && _step != Step.Over)
            {
                Conclude(now);   // packed up early: what was landed still counts
            }
        }

        private void Pull(float now)
        {
            // Each pull the zone moves and the fish is a little quicker.
            _sweep.Begin(now, 1.05f - 0.12f * _pulls, Random.Range(0.25f, 0.75f), _fish.Zone, _fish.Zone * 0.3f);
            Meter = true;
            ZoneCentre = _sweep.Centre;
            ZoneWidth = _sweep.Width;
            SweetWidth = _sweep.Sweet;
        }

        public override void Tick(float now)
        {
            switch (_step)
            {
                case Step.Fly:
                    if (now >= _stepEnds)
                    {
                        _step = Step.Wait;
                        // A good cast lands where the fish are.
                        _stepEnds = now + Random.Range(5f, 13f) * (_castQuality == 2 ? 0.6f : _castQuality == 1 ? 0.8f : 1.15f);
                        Objective = T("reallife_leisure_fishing_waiting");
                        Prompt = null;
                        OwnsInteractKey = true;
                        QuietPrompt = T("reallife_leisure_fishing_prompt_strike");
                        Rod(WaitClip);   // the cast ends in this loop's first pose
                    }
                    break;

                case Step.Wait:
                    if (now >= _stepEnds)
                    {
                        _fish = Roll(_castQuality);
                        _grams = Random.Range(_fish.MinGrams, _fish.MaxGrams + 1);
                        _step = Step.Bite;
                        _stepEnds = now + _fish.Window;
                        Objective = T("reallife_leisure_fishing_bite");
                        Prompt = T("reallife_leisure_fishing_prompt_strike");
                        LeisureSound.Cue(LeisureCue.Bite);
                    }
                    break;

                case Step.Bite:
                    if (now >= _stepEnds)
                    {
                        Prompt = null;
                        Objective = T("reallife_leisure_fishing_missed");
                        NextCast(now, 2.5f);
                    }
                    break;

                case Step.Landed:
                    if (now >= _stepEnds)
                    {
                        _cast++;
                        if (_cast >= Casts)
                        {
                            Conclude(now);
                        }
                        else
                        {
                            ToReady(now);
                        }
                    }
                    break;

                case Step.Over:
                    if (now >= _stepEnds)
                    {
                        Finish(true);
                    }
                    break;
            }
        }

        private void NextCast(float now, float after, bool landing = false)
        {
            Meter = false;
            Prompt = null;
            OwnsInteractKey = false;
            QuietPrompt = null;
            _step = Step.Landed;
            _stepEnds = now + after;
            Stow();
            if (!landing)
            {
                Rod(WaitClip);
            }
        }

        private void Land(float now)
        {
            _landed++;
            int value = Mathf.RoundToInt(_grams / 1000f * _fish.ValuePerKilo);
            _sold += value;
            bool first = LeisureState.Catch(_fish.Id);
            _newKind |= first && _fish.ValuePerKilo > 0;
            if (_fish.ValuePerKilo > 0 && _grams > _bestGrams)
            {
                _bestGrams = _grams;
            }
            Objective = T(first ? "reallife_leisure_fishing_landed_first" : "reallife_leisure_fishing_landed",
                "fish", T(_fish.NameKey), "weight", Kilos(_grams));
            LeisureSound.Cue(LeisureCue.Good);
            LeisureMoney.Tire(0.8f);
            NextCast(now, 3.2f, Rod(LandClip));   // fish_land ends holding the rod out
        }

        private void Conclude(float now)
        {
            _step = Step.Over;
            _stepEnds = now + 0.5f;
            Meter = false;
            Prompt = null;
            Choices = null;
            OwnsInteractKey = false;
            QuietPrompt = null;
            Stow();
            Ctx.Body.Rest(0.4f);   // the rod down before the hold is let go
            Score = _bestGrams;
            if (_sold > 0)
            {
                LeisureMoney.Give(_sold, "reallife_transaction_fishing_catch");
            }
            CardTitle = T("reallife_leisure_fishing_title");
            CardBody = _landed == 0
                ? T("reallife_leisure_fishing_result_none")
                : T("reallife_leisure_fishing_result", "count", _landed.ToString(), "amount", JobText.Money(_sold))
                  + (_bestGrams > 0 ? "\n" + T("reallife_leisure_fishing_result_best", "weight", Kilos(_bestGrams)) : string.Empty)
                  + "\n" + T("reallife_leisure_fishing_result_kinds", "kinds", LeisureState.FishKinds.ToString(), "all", Catch.Length.ToString());
            Mood = _newKind || _bestGrams >= 3000 ? ModState.MoodUpBig : _landed > 0 ? ModState.MoodUp : null;
            _sold = 0;   // paid: End must not pay again
        }

        private static string Kilos(int grams)
        {
            return (grams / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " kg";
        }

        // A better cast shifts the odds away from the commonest.
        private static Fish Roll(int quality)
        {
            int total = 0;
            for (int i = 0; i < Catch.Length; i++)
            {
                total += Odds(i, quality);
            }
            int pick = Random.Range(0, total);
            for (int i = 0; i < Catch.Length; i++)
            {
                pick -= Odds(i, quality);
                if (pick < 0)
                {
                    return Catch[i];
                }
            }
            return Catch[0];
        }

        private static int Odds(int index, int quality)
        {
            int weight = Catch[index].Weight;
            return index >= 3 ? weight * (1 + quality) : weight * 2;
        }

        public override void Frame(float now, float delta)
        {
            if (_step == Step.Ready || _step == Step.Fight)
            {
                MeterCursor = _sweep.At(now);
            }
            Transform rod = Ctx.Body.Tool;
            if (rod == null)
            {
                return;
            }
            if (!_rodClips)
            {
                // The stand-in aims the rod out over the water, tip up, more upright while a fish is on.
                Vector3 ahead = _water - rod.position;
                ahead.y = 0f;
                float lift = _step == Step.Fight ? 1.1f : _step == Step.Ready || _step == Step.Landed ? 1.6f : 0.55f;
                rod.rotation = Quaternion.Slerp(rod.rotation, Quaternion.LookRotation(ahead.normalized + Vector3.up * lift), 6f * delta);
            }
            if (_float == null || _line == null)
            {
                return;
            }
            // The line is out from the cast's release until it is wound in.
            bool lineOut = (_step == Step.Fly && now >= _flyFrom) || _step == Step.Wait || _step == Step.Bite || _step == Step.Fight;
            if (!lineOut)
            {
                return;
            }
            Vector3 tip = rod.position + rod.forward * RodLength;
            Vector3 at;
            if (_step == Step.Fly)
            {
                float t = Mathf.Clamp01((now - _flyFrom) / FlySeconds);
                at = Vector3.Lerp(tip, _water, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.4f);
            }
            else
            {
                float dip = _step == Step.Bite ? 0.09f : _step == Step.Fight ? 0.04f + 0.03f * Mathf.Sin(now * 9f) : 0.012f * Mathf.Sin(now * 1.7f);
                _floatDip = Mathf.MoveTowards(_floatDip, dip, 0.5f * delta);
                at = _water + Vector3.down * _floatDip;
            }
            if (!_float.gameObject.activeSelf)
            {
                _float.gameObject.SetActive(true);
                _line.gameObject.SetActive(true);
            }
            _float.position = at;
            Vector3 span = at - tip;
            float length = span.magnitude;
            if (length > 0.05f)
            {
                _line.SetPositionAndRotation(tip, Quaternion.LookRotation(span / length));
                _line.localScale = new Vector3(1f, 1f, length);
            }
        }

        // The float and line hidden between casts; they are still ours.
        private void Stow()
        {
            if (_float != null && _float.gameObject.activeSelf)
            {
                _float.gameObject.SetActive(false);
            }
            if (_line != null && _line.gameObject.activeSelf)
            {
                _line.gameObject.SetActive(false);
            }
        }

        public override void End()
        {
            if (_float != null)
            {
                _float.gameObject.SetActive(true);   // Return skips a switched-off prop as returned already
                Ctx.Props.Return(_float);
                _float = null;
            }
            if (_line != null)
            {
                _line.gameObject.SetActive(true);
                Ctx.Props.Return(_line);
                _line = null;
            }
            if (!Played && _sold > 0)
            {
                LeisureMoney.Give(_sold, "reallife_transaction_fishing_catch");   // walked off: the fish are still sold
            }
        }
    }
}
