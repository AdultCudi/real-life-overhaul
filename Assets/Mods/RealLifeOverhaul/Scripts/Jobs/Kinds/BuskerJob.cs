using System;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.Characters;
using Entities;
using Helpers;
using Streets.Pedestrians;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Busking on a pitch taken by walking on to it (<see cref="JobStand"/>): the player picks one of three songs on the
    /// panel over the pitch, then hits Strum in each moment a strum is called for. Tips come at a song's end by the crowd, the
    /// hour, the song and the playing; a passer-by may stop to listen, and the police may move the busker on. The rest of the
    /// set can be left to the game's clock (<see cref="ShiftClock"/>). Played with the mod's guitar clip or the game's act.
    /// </summary>
    public sealed partial class BuskerJob : Job, IClockedShift
    {
        private enum Step { ToPitch, Pick, Song }

        /// <summary>Seconds away from the pitch before the set is packed up; what is in the case is kept.</summary>
        public const float AbandonAfter = 25f;

        /// <summary>The share of a song's strums a player in time is taken to hit, for what the clock pays.</summary>
        public const float AveragePlay = 0.6f;

        private const float Debounce = 0.7f;       // seconds after a song ends in which the panel's buttons do nothing (a key meant for a strum)
        private const float PanelUp = 2.05f;       // metres over the pitch the panel stands
        private const string ButtonStrum = "reallife_job_busker_button_strum";
        private const string ButtonMore = "reallife_job_busker_button_more";
        private const string ButtonBack = "reallife_job_busker_button_back";
        private const string ButtonSkip = "reallife_job_button_skip";
        private const string ButtonAway = "reallife_job_button_away";
        private const float PitchReach = 1.0f;      // metres
        private const float SongSeconds = 40f;      // seconds
        private const float CrowdWithin = 28f;      // metres
        private const int CrowdFull = 8;            // people about for the fullest tips
        private const float MoveOnChance = 0.12f;   // a chance after each song once SongsBeforeMoveOn are played
        private const int SongsBeforeMoveOn = 2;
        private const string StrumClip = "guitar_strum";
        private const string CaseProp = "guitar_case";
        private const float WellPlayed = 0.6f;           // the quality at which a song is good (the toast and the cheer)

        // Per song: seconds between strums, seconds to hit one, and what the song pays by.
        private static readonly float[] Gap = { 3.3f, 2.5f, 1.8f };
        private static readonly float[] Window = { 1.2f, 0.95f, 0.75f };
        private static readonly float[] Worth = { 0.85f, 1f, 1.25f };
        private static readonly string[] SongKeys = { "reallife_job_busker_song_1", "reallife_job_busker_song_2", "reallife_job_busker_song_3" };
        private static readonly string[] Good = { "reallife_job_busker_good_1", "reallife_job_busker_good_2", "reallife_job_busker_good_3" };
        private static readonly string[] Poor = { "reallife_job_busker_poor_1", "reallife_job_busker_poor_2" };

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Busker);
        private readonly string[] _songNames = new string[JobKeys.Choices];
        private readonly Action[] _onSong = new Action[JobKeys.Choices];
        private readonly JobPanel _panel = new JobPanel("busker panel");
        private Action _onStrum;
        private Action _onMore;
        private Action _onSkip;
        private Action _onAway;
        private ShiftFigures _figures;
        private ShiftClock _clock;

        private Step _step;
        private string _idle;                 // the looping clip on the pitch, or null for the game's own standing
        private bool _more;                   // the panel shows Step away and Let time pass in place of the songs
        private float _songOver = -10f;
        private float _awaySince = -1f;
        private int _handSongs;               // by hand; the clock's are paid as they end
        private int _clockTips;               // what the clock's songs were paid
        private float _clockEach;             // worked out when the clock was offered
        private string _objPitch;
        private string _objBack;
        private string _objFirst;
        private string _objNext;
        private string _objClock;
        private string _panelPick;
        private string _panelMore;
        private string _panelNow;
        private string _panelWait;
        private string _panelSongs;
        private string _panelStrums;
        private Vector3 _pitch;
        private Vector3 _facing;
        private Transform _case;
        private bool _playing;
        private int _songsWanted;
        private int _songs;
        private int _song;
        private float _songEnds;
        private float _nextBeat;
        private float _beatUntil;
        private bool _beatOpen;
        private int _beats;
        private int _hits;
        private int _wrong;
        private int _tips;
        private float _nextHint;
        private bool _paid;
        private int _line = -1;
        private string _pitchLabel;

        public override JobDef Def => _def;

        public override bool PassingTime => _clock != null && _clock.Working;

        public override bool OwnsInteractKey => Ctx.Stand.Standing;

        int IClockedShift.ClockLeft => Mathf.Max(0, _songsWanted - _songs);

        float IClockedShift.ClockPay => _clockEach;

        protected override bool OnBegin(float now)
        {
            if (Ctx.Driving || PlayerHelper.IsUsingVehicle)
            {
                Refusal = "reallife_job_on_foot";
                return false;
            }
            _songsWanted = ShiftSize();
            if (_songsWanted <= 0)
            {
                Refusal = "reallife_job_no_daily";
                return false;
            }
            bool ownClip = ClipLibrary.Ready && ClipLibrary.TryGet(StrumClip, out CityClip strum) && strum.HasTool && JobProps.Has(strum.Prop);
            _act = GameAct(PermanentAnimationType.PlayingGuitar) ?? (ownClip ? null : GameAct(PermanentAnimationType.Singing));
            if (!ownClip && _act == null)
            {
                Refusal = "reallife_job_no_here";
                Ctx.Logger?.Info("Jobs: no busking: neither the strumming clip nor one of the game's own performances can be played on the player.");
                return false;
            }
            // Back to a wall where there is one near; otherwise facing across the pavement.
            Vector3 near = Ctx.PlayerPosition;
            bool atWall = CitySpots.Shopfront(near, 0.9f, 0.7f, out _pitch, out _facing) && JobSpots.Flat(_pitch, near) < 12f;
            if (!atWall)
            {
                if (!CitySpots.Pavement(near, 8f, out Vector3 found) || !CitySpots.Stand(found, 0.8f, out _pitch) || !CitySpots.Along(_pitch, out Vector3 along))
                {
                    Refusal = "reallife_job_busker_no_room";
                    return false;
                }
                _facing = Vector3.Cross(Vector3.up, along);
                if (!CitySpots.OnPavement(_pitch + _facing * 1.1f, out _))
                {
                    _facing = -_facing;
                }
            }
            for (int i = 0; i < JobKeys.Choices; i++)
            {
                int song = i;
                _songNames[i] = JobText.T(SongKeys[i]);
                _onSong[i] = () => Choose(song, Time.time);
            }
            _onStrum = () => Strum(Time.time);
            _onMore = More;
            _onSkip = OfferSkip;
            _onAway = StepAway;
            _pitchLabel = JobText.T("reallife_job_busker_marker_pitch");
            _objPitch = JobText.T("reallife_job_busker_obj_pitch");
            _objBack = JobText.T("reallife_job_busker_obj_back");
            _objFirst = JobText.T("reallife_job_busker_obj_first");
            _objNext = JobText.T("reallife_job_busker_obj_next");
            _objClock = JobText.T("reallife_job_busker_obj_clock");
            _panelPick = JobText.T("reallife_job_busker_panel_pick");
            _panelMore = JobText.T("reallife_job_busker_panel_more");
            _panelNow = JobText.T("reallife_job_busker_panel_now");
            _panelWait = JobText.T("reallife_job_busker_panel_wait");
            _panelSongs = JobText.T("reallife_job_busker_panel_songs");
            _panelStrums = JobText.T("reallife_job_busker_panel_strums");
            _idle = ownClip ? StrumClip : null;
            _step = Step.ToPitch;
            _figures = new ShiftFigures(Ctx, Def, "reallife_job_busker_card_done");
            _clock = new ShiftClock(Ctx, this, this, "busker", "reallife_job_busker_unit", "reallife_job_busker_units", "reallife_job_busker_slip", true);
            Ctx.Stand.Place(_pitch, _facing, PitchReach, _idle);
            Mark(_pitch, _pitchLabel);
            Show(_objPitch);
            Figures(now);
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: busker: the pitch is " + JobSpots.Flat(_pitch, Ctx.PlayerPosition).ToString("F1", CultureInfo.InvariantCulture) + " m from the player "
                                 + (atWall ? "with its back to a wall" : "on open pavement")
                                 + " (the zone that takes the player is " + PitchReach.ToString("F1", CultureInfo.InvariantCulture) + " m round it); " + _songsWanted
                                 + " songs; shown in the game's own interface: job panel "
                                 + (_figures.Shown ? "copied" : "NOT copied (the panel over the pitch is all there is)") + ", the game's guider points at the place.");
            }
            return true;
        }

        public override void Tick(float now)
        {
            if (_clock.Hold())
            {
                Figures(now);   // the clock is working, or its offer is up with the game paused
                return;
            }
            if (_step == Step.Pick && _songs >= _songsWanted)
            {
                _panel.Hide();   // the last songs were left to the clock
                Conclude();
                return;
            }
            bool onFoot = !Ctx.Driving && !PlayerHelper.IsUsingVehicle;
            bool standing = Ctx.Stand.Tick(Ctx.Player, onFoot, now);
            if (standing)
            {
                if (_step == Step.ToPitch && Ctx.Stand.Ready)
                {
                    SetUp(now);
                }
                if (HasMarker)
                {
                    Unmark();
                }
                _awaySince = -1f;
            }
            else
            {
                _panel.Hide();
                _panelState = int.MinValue;
                if (_step != Step.ToPitch)
                {
                    LeftPitch(now);
                    if (Over)
                    {
                        return;
                    }
                }
                if (!HasMarker)
                {
                    Mark(_pitch, _pitchLabel);
                }
            }
            switch (_step)
            {
                case Step.Pick:
                    Listener(now);
                    break;
                case Step.Song:
                    Song(now);
                    Listener(now);
                    break;
            }
            if (Over)
            {
                return;
            }
            Figures(now);
            if (standing && Ctx.Stand.Ready && _step != Step.ToPitch)
            {
                Panel(now);
            }
        }

        // Off the pitch: the music stops and a song in the air is lost; too long away packs the set up.
        private void LeftPitch(float now)
        {
            if (_playing)
            {
                Music(false);
                _playing = false;
                Ctx.Self.Rest();
                Unlook();
                if (_step == Step.Song)
                {
                    _beatOpen = false;
                    ToPick();
                    Ctx.Logger?.Info("Jobs: busker: the player left the pitch in the middle of a song; the song is not counted.");
                }
            }
            if (_awaySince < 0f)
            {
                _awaySince = now;
            }
            else if (now - _awaySince > AbandonAfter)
            {
                Ctx.Say("reallife_job_busker_abandoned", 8f);
                Conclude();
            }
        }

        private void Song(float now)
        {
            if (_playing && !Ctx.Self.Working)
            {
                StartPlaying();   // the clip service was switched off: the next way of playing takes over
            }
            if (_beatOpen && now > _beatUntil)
            {
                _beatOpen = false;   // the moment passed
                _nextBeat = now + Gap[_song] * Random.Range(0.7f, 1.1f);
            }
            if (now >= _songEnds)
            {
                EndSong(now);
                return;
            }
            if (!_beatOpen && now >= _nextBeat && _songEnds - now > Window[_song])
            {
                _beatOpen = true;
                _beats++;
                _beatUntil = now + Window[_song];
            }
        }

        // The panel's Strum, run by the game on its key or a click: in time if a strum is called for, else a wrong note.
        private void Strum(float now)
        {
            if (Over || _step != Step.Song || !Ctx.Stand.Ready)
            {
                return;
            }
            if (_beatOpen)
            {
                _beatOpen = false;
                _hits++;
                _nextBeat = now + Gap[_song] * Random.Range(0.7f, 1.1f);
            }
            else
            {
                _wrong++;
                if (now >= _nextHint)
                {
                    _nextHint = now + 5f;
                    Ctx.Say("reallife_job_busker_wrong", 3f);
                }
            }
        }

        public override void Choose(int choice, float now)
        {
            if (Over || _step != Step.Pick || _more || !Ctx.Stand.Ready || _clock.Offered || now - _songOver < Debounce)
            {
                return;
            }
            _song = choice;
            if (!_playing && !StartPlaying())
            {
                Ctx.Say("reallife_job_busker_no_play", 7f);
                Conclude();
                return;
            }
            _beats = 0;
            _hits = 0;
            _wrong = 0;
            _beatOpen = false;
            _songEnds = now + SongSeconds;
            _nextBeat = now + 2.5f;
            _step = Step.Song;
            _panelState = int.MinValue;
            Show(JobText.T("reallife_job_busker_obj_song", "song", _songNames[choice]));
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: busker: song " + (_songs + 1) + " of " + _songsWanted + " begun: " + SongKeys[choice] + ", a strum every "
                                 + Gap[choice].ToString("F1", CultureInfo.InvariantCulture) + " s, "
                                 + Window[choice].ToString("F2", CultureInfo.InvariantCulture) + " s to hit it, worth x" + Worth[choice].ToString("F2", CultureInfo.InvariantCulture) + ", "
                                 + SongSeconds.ToString("F0") + " s long.");
            }
        }

        // The case goes down in front; the stand has already turned the player to the pavement.
        private void SetUp(float now)
        {
            Unmark();
            Vector3 front = Ctx.Player.transform.position + _facing * 1.0f;
            if (CitySpots.OnPavement(front, out Vector3 ground) && CitySpots.Clear(ground, 0.3f))
            {
                _case = Ctx.Props.Stand(CaseProp, ground, Vector3.Cross(Vector3.up, _facing));   // lying across in front of them
            }
            _nextListener = now + 8f;
            _songOver = now;
            ToPick();
            Ctx.Logger?.Info("Jobs: busker: set up on the pitch, " + (_case != null ? "the case is down" : "no room for the case") + ", " + _songsWanted + " songs to play.");
        }

        private void ToPick()
        {
            _step = Step.Pick;
            _more = false;
            _panelState = int.MinValue;
            Show(_songs == 0 ? _objFirst : _objNext);
        }

        /// <summary>What a song usually brings in, for the clock's share: the middle song, played at <see cref="AveragePlay"/>.</summary>
        private float AverageTip() => SongTip(Crowd(), 1, AveragePlay);

        // What a song brings in by the crowd, the hour, the song and the strums hit, times the offer's crowd factor.
        private float SongTip(int crowd, int song, float played)
        {
            float street = 0.5f + Mathf.Min(crowd, CrowdFull) / (float)CrowdFull;
            return Def.UnitPay * Ctx.Rate * street * HourFactor(TimeHelper.CurrentHour) * Worth[song] * (0.3f + played) * Ctx.TipFactor * Ctx.Offer.CrowdFactor;
        }

        private void OfferSkip()
        {
            if (Over || _step != Step.Pick || !_more || _songs >= _songsWanted || Time.time - _songOver < Debounce)
            {
                return;
            }
            _clockEach = AverageTip();
            if (_clock.Offer())
            {
                _panel.Hide();   // the game's activity panel has the keys while it is up
                _panelState = int.MinValue;
            }
        }

        void IClockedShift.ClockStarted()
        {
            // The street is cleared: a listener walks on, and the music stops until the player plays again.
            ReleaseListener();
            Music(false);
            _playing = false;
        }

        void IClockedShift.ClockEnded(int count, int pay, float away)
        {
            _songs += count;
            _clockTips += pay;
            _songOver = Time.time;
            _more = false;
            _panelState = int.MinValue;
            _nextListener = Time.time + 8f;
            if (_songs >= _songsWanted)
            {
                Ctx.Say("reallife_job_busker_done", 7f);
            }
        }

        private void EndSong(float now)
        {
            _beatOpen = false;
            float quality = Mathf.Clamp01((_beats > 0 ? (float)_hits / _beats : 0f) - _wrong * 0.06f);
            int crowd = Crowd();
            int tip = Mathf.RoundToInt(SongTip(crowd, _song, quality));
            _tips += tip;
            _cheerDue = _listener.Held && quality >= WellPlayed && ClipLibrary.Has(CheerClip);
            _songs++;
            _handSongs++;
            UnitDone(_handSongs, UnitPay());   // the set's big tipper, if the offer drew one
            _songOver = now;
            Ctx.Logger?.Info("Jobs: busking song " + _songs + ": " + _hits + " of " + _beats + " strums, " + _wrong + " wrong notes, " + crowd
                             + " people about, hour " + TimeHelper.CurrentHour + ": " + tip + " in the case.");
            JobText.Toast(UI.Notification.NotificationType.Info, JobText.Pick(quality >= WellPlayed ? Good : Poor, ref _line), "amount", JobText.Money(tip), 6f);

            if (_songs >= _songsWanted)
            {
                Ctx.Say("reallife_job_busker_done", 7f);
                Conclude();
            }
            else if (_songs >= SongsBeforeMoveOn && Settings.Police && Random.value < MoveOnChance)
            {
                Ctx.Say("reallife_job_busker_moved_on", 9f);
                Conclude();
            }
            else if (SaveGameManager.Current.Energy < JobCatalogue.MinEnergy)
            {
                Ctx.Say("reallife_job_done_tired", 9f);
                Conclude();
            }
            else
            {
                ToPick();
            }
        }

        // Mornings are thin, lunch and the evening are when people have time and change, the small hours are dead.
        private static float HourFactor(int hour)
        {
            if (hour >= 23 || hour < 6)
            {
                return 0.4f;
            }
            if (hour >= 16 && hour < 21)
            {
                return 1.4f;
            }
            if (hour >= 11 && hour < 14)
            {
                return 1.2f;
            }
            return hour < 9 ? 0.8f : 1f;
        }

        private int Crowd()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            int people = 0;
            Vector3 here = Ctx.PlayerPosition;
            for (int i = 0; walkers != null && i < walkers.Count && people < CrowdFull; i++)
            {
                if (walkers[i] != null && (walkers[i].transform.position - here).sqrMagnitude < CrowdWithin * CrowdWithin)
                {
                    people++;
                }
            }
            return people;
        }

        private void Conclude()
        {
            Settle();
            Finish(JobEnd.Done);
        }

        private void Settle()
        {
            if (!Owed(out PaySlip slip))
            {
                return;
            }
            _paid = true;
            Ctx.Pay(this, slip, "reallife_job_busker_slip");
        }

        protected override bool Owed(out PaySlip slip)
        {
            slip = default;
            if (_paid || _songs <= 0 || (_handSongs <= 0 && !(Ctx.Gig != null && _songs * 2 >= _songsWanted)))
            {
                return false;
            }
            // A booked set adds the venue's fee when at least half was played; the clock's songs were paid as it ended.
            slip = new PaySlip { Units = _handSongs, Tips = _tips, Bonus = Ctx.Gig != null && _songs * 2 >= _songsWanted ? Ctx.Gig.Fee : 0 };
            return true;
        }

        protected override ShiftClock Clocked => _clock;

        public override void End(JobEnd why)
        {
            _panel.Hide();
            _clock?.Close();   // the game's activity panel goes; nothing more is settled through it
            Music(false);
            ReleaseListener();
            _playing = false;
            Ctx.Self.Rest();
            if (_case != null)
            {
                Ctx.Props.Return(_case);
                _case = null;
            }
            if (why != JobEnd.Fault)
            {
                Settle();
            }
        }
    }
}
