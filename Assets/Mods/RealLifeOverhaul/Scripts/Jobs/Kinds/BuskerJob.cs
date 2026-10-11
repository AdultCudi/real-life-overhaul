using System;
using System.Collections.Generic;
using BigAmbitions.Characters;
using Characters.EmojiSystem;
using Entities;
using Helpers;
using RealLifeOverhaul.GameUi;
using Streets.Pedestrians;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Busking. A pitch on the pavement where the player stands, back to a wall if there is
    /// one: the case goes down in front, and the player plays. Before each song they pick one of
    /// three (slow and forgiving, middling, fast and better paid); during it a strum is called
    /// for every few seconds and the interaction key must come inside the moment it is asked
    /// for. A press at any other time is a wrong note. Tips come at the end of each song, by how
    /// many people are about, the time of day, the song and how it was played; now and then a
    /// passer-by stops to listen and drops something in the case. After a couple of songs the
    /// police may move the busker on, which ends the shift with everything earned kept.
    ///
    /// The playing, in order of preference: the mod's <c>guitar_strum</c> clip with the modelled
    /// guitar at the clip's grip; the game's own guitar performance, exactly as its street
    /// performers do it (its animation, its guitar where its data puts it); the game's singing,
    /// which needs no instrument. The music is the game's own for that performance. The player
    /// is held on the pitch from setting up to packing up. Listeners are the game's pedestrians,
    /// borrowed one at a time and handed back walking (PedestrianTakeover), as with flyers.
    ///
    /// IN THE GAME'S OWN INTERFACE. The pitch is a place of work the player takes by walking on to it
    /// (<see cref="JobStand"/>): nothing is pressed, they are turned to face the pavement, held there and put in the
    /// guitar pose. The game's panel over a place (<see cref="JobPanel"/>) stands over the pitch: the three songs are its
    /// first three buttons, "Strum" is its first button while a song is on, and "More" shows "Step away" and
    /// "Let time pass", which hands the rest of the set to the game's clock (<see cref="ShiftClock"/>):
    /// <see cref="ShiftRates"/> minutes a song, at <see cref="ShiftRates.Share"/> of what a song usually brings in
    /// (<see cref="AverageTip"/>), no bonus. The figures are on the HUD in a copy of the game's job panel
    /// (<see cref="ShiftFigures"/>). The game has no busker's outfit, so the player plays in their own clothes.
    /// </summary>
    public sealed class BuskerJob : Job, IClockedShift
    {
        private enum Step { ToPitch, Pick, Song }

        /// <summary>Seconds away from the pitch before the set is packed up (what is in the case is kept).</summary>
        public const float AbandonAfter = 25f;

        /// <summary>The share of a song's strums a player in time is taken to hit, for what the clock pays: what a song usually brings in.</summary>
        public const float AveragePlay = 0.6f;

        private const float Debounce = 0.7f;       // seconds after a song ends in which the panel's buttons do nothing (a key meant for a strum)
        private const float PanelUp = 2.05f;       // metres over the pitch the panel stands
        private const string ButtonStrum = "reallife_job_busker_button_strum";
        private const string ButtonMore = "reallife_job_busker_button_more";
        private const string ButtonBack = "reallife_job_busker_button_back";
        private const string ButtonSkip = "reallife_job_button_skip";
        private const string ButtonAway = "reallife_job_button_away";
        private const float PitchReach = 1.0f;
        private const float SongSeconds = 40f;
        private const float CrowdWithin = 28f;
        private const int CrowdFull = 8;
        private const float ListenWithin = 7f;
        private const float MoveOnChance = 0.12f;
        private const int SongsBeforeMoveOn = 2;
        private const string StrumClip = "guitar_strum";
        private const string CaseProp = "guitar_case";

        // gap between strums, how long the moment lasts, what the song pays by
        private static readonly float[] Gap = { 3.3f, 2.5f, 1.8f };
        private static readonly float[] Window = { 1.2f, 0.95f, 0.75f };
        private static readonly float[] Worth = { 0.85f, 1f, 1.25f };
        private static readonly string[] SongKeys = { "reallife_job_busker_song_1", "reallife_job_busker_song_2", "reallife_job_busker_song_3" };
        private static readonly string[] Good = { "reallife_job_busker_good_1", "reallife_job_busker_good_2", "reallife_job_busker_good_3" };
        private static readonly string[] Poor = { "reallife_job_busker_poor_1", "reallife_job_busker_poor_2" };

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Busker);
        private readonly PedestrianTakeover _listener = new PedestrianTakeover();
        private readonly string[] _songNames = new string[JobKeys.Choices];
        private readonly Action[] _onSong = new Action[JobKeys.Choices];
        private readonly JobPanel _panel;
        private Action _onStrum;
        private Action _onMore;
        private Action _onSkip;
        private Action _onAway;
        private ShiftFigures _figures;
        private ShiftClock _clock;

        private Step _step;
        private string _idle;                 // the looping clip the player waits in on the pitch, or null for the game's own standing
        private bool _more;                   // the panel shows "Step away" and "Let time pass" in place of the songs
        private float _songOver = -10f;
        private float _awaySince = -1f;
        private int _handSongs;               // songs played by hand (the clock's are paid as they end)
        private int _clockTips;               // what the clock's songs were paid
        private float _clockEach;             // what the clock pays a song, worked out when it was offered
        private int _panelState = int.MinValue;
        private int _cardStrums = int.MinValue;
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
        private AudioSource _music;
        private StreetPerformerData _act;     // the game's own performance used for the look and the music, or null
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
        private float _listenUntil;
        private float _nextListener;
        private bool _paid;
        private int _line = -1;
        private string _pitchLabel;

        public BuskerJob()
        {
            _panel = new JobPanel("busker panel");
        }

        public override JobDef Def => _def;

        public override bool PassingTime => _clock != null && _clock.Working;

        /// <summary>While the player is on the pitch the panel's first button is on the game's interact key: the mod's own use of that key stands aside.</summary>
        public override bool OwnsInteractKey => Ctx != null && Ctx.Stand.Standing;

        int IClockedShift.ClockLeft => Mathf.Max(0, _songsWanted - _songs);

        float IClockedShift.ClockPay => _clockEach;

        protected override bool OnBegin(float now)
        {
            if (Ctx.Driving || PlayerHelper.IsUsingVehicle)
            {
                Refusal = "reallife_job_on_foot";
                return false;
            }
            _songsWanted = ShiftSize();   // the offer's set
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
            // Back to a wall where there is one near; otherwise where they stand, facing across the pavement.
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
                Ctx.Logger?.Info("Jobs: busker: the pitch is " + JobSpots.Flat(_pitch, Ctx.PlayerPosition).ToString("F1") + " m from the player " + (atWall ? "with its back to a wall" : "on open pavement")
                                 + " (the zone that takes the player is " + PitchReach.ToString("F1") + " m round it); " + _songsWanted + " songs; shown in the game's own interface: job panel "
                                 + (_figures.Shown ? "copied" : "NOT copied (the panel over the pitch is all there is)") + ", the game's guider points at the place.");
            }
            return true;
        }

        // One of the game's own street performances that the player's animator can play, or null.
        private StreetPerformerData GameAct(PermanentAnimationType animation)
        {
            try
            {
                if (!Ctx.Self.Knows(animation))
                {
                    return null;
                }
                IReadOnlyList<StreetPerformerData> acts = StreetPerformerDataHelper.GetStreetPerformerDataList();
                for (int i = 0; acts != null && i < acts.Count; i++)
                {
                    if (acts[i] != null && acts[i].animation == animation)
                    {
                        return acts[i];
                    }
                }
            }
            catch (Exception e)
            {
                Ctx.Logger?.Error(e);
            }
            return null;
        }

        // ---------------------------------------------------------------- the tick

        public override void Tick(float now)
        {
            if (_clock.Hold())
            {
                Figures(now);   // the clock is doing the work (or the offer is up, with the game paused)
                return;
            }
            if (_step == Step.Pick && _songs >= _songsWanted)
            {
                _panel.Hide();   // (the last of the songs were left to the clock)
                Conclude(false);
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

        // Off the pitch: the music stops and a song in the air is lost; too long away and the set is packed up (what is in the case is kept).
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
                Conclude(false);
            }
        }

        private void Song(float now)
        {
            if (_playing && !Ctx.Self.Working)
            {
                StartPlaying();   // the clip service was switched off under us: the next way of playing takes over
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

        // The panel's "Strum" (the game ran it on its key or on a click): in time if a strum is called for, else a wrong note.
        private void Strum(float now)
        {
            if (Ctx == null || Over || _step != Step.Song || !Ctx.Stand.Ready)
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

        /// <summary>One of the panel's three song buttons (the game ran it on its key or on a click): 0, 1 or 2.</summary>
        public override void Choose(int choice, float now)
        {
            if (Ctx == null || Over || _step != Step.Pick || _more || !Ctx.Stand.Ready || _clock.Offered || now - _songOver < Debounce)
            {
                return;
            }
            _song = choice;
            if (!_playing && !StartPlaying())
            {
                Ctx.Say("reallife_job_busker_no_play", 7f);
                Conclude(false);
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
                Ctx.Logger?.Info("Jobs: busker: song " + (_songs + 1) + " of " + _songsWanted + " begun: " + SongKeys[choice] + ", a strum every " + Gap[choice].ToString("F1") + " s, "
                                 + Window[choice].ToString("F2") + " s to hit it, worth x" + Worth[choice].ToString("F2") + ", " + SongSeconds.ToString("F0") + " s long.");
            }
        }

        // The case down in front. The stand has already turned the player to face the pavement and held them there.
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

        // ---------------------------------------------------------------- the game's panel over the pitch

        // Songs while one is to be picked, the strum while one is played; "More" has "Step away" and "Let time pass". A text or a
        // button is made only when what it says has changed.
        private void Panel(float now)
        {
            int songs = _songsWanted - _songs;
            int state;
            if (_step == Step.Song)
            {
                state = 1 | (_beatOpen ? 2 : 0) | (_hits << 4) | (_beats << 12);
            }
            else
            {
                state = 2 | (_more ? 4 : 0) | (songs << 4);
            }
            if (state != _panelState)
            {
                _panelState = state;
                if (_step == Step.Song)
                {
                    _panel.Panel.Lines(_beatOpen ? _panelNow : _panelWait, _panelStrums, JobText.T("reallife_job_busker_panel_hits", "hits", _hits.ToString(), "beats", _beats.ToString()),
                        _beatOpen ? WorldPanel.Green() : _panel.White);
                    _panel.Panel.Button(0, ButtonStrum, _onStrum, true, true);
                    _panel.Panel.Button(1, null, null, false, false);
                    _panel.Panel.Button(2, null, null, false, false);
                    _panel.Panel.Button(3, null, null, false, false);
                }
                else if (_more)
                {
                    _panel.Panel.Lines(_panelMore, _panelSongs, JobText.Number(_songs) + "/" + JobText.Number(_songsWanted), _panel.White);
                    _panel.Panel.Button(0, null, null, false, false);
                    _panel.Panel.Button(1, ButtonAway, _onAway, true, false);
                    _panel.Panel.Button(2, ButtonSkip, _onSkip, songs > 0, true);
                    _panel.Panel.Button(3, ButtonBack, _onMore, true, false);
                }
                else
                {
                    _panel.Panel.Lines(_panelPick, _panelSongs, JobText.Number(_songs) + "/" + JobText.Number(_songsWanted), _panel.White);
                    for (int i = 0; i < JobKeys.Choices; i++)
                    {
                        _panel.Panel.Button(i, SongKeys[i], _onSong[i], true, true);
                    }
                    _panel.Panel.Button(3, ButtonMore, _onMore, true, false);
                }
            }
            _panel.Show(_pitch + Vector3.up * PanelUp, Ctx.Player);
        }

        private void More()
        {
            if (Ctx == null || Over || _step != Step.Pick || Time.time - _songOver < Debounce)
            {
                return;
            }
            _more = !_more;
            _panelState = int.MinValue;
        }

        private void StepAway()
        {
            if (Ctx == null || Over || _step != Step.Pick || !_more || Time.time - _songOver < Debounce)
            {
                return;
            }
            Ctx.Stand.StepAway();   // the music stops and the shift waits on the pitch (see LeftPitch)
            _panel.Hide();
            _panelState = int.MinValue;
            Show(_objBack);
        }

        // ---------------------------------------------------------------- the figures on the HUD

        // Five lines of the game's job panel, each written only when one of its figures changes.
        private void Figures(float now)
        {
            bool clock = _clock.Working;
            if (clock)
            {
                Show(_objClock);
            }
            else if (_step != Step.ToPitch && !Ctx.Stand.Standing)
            {
                Show(_objBack);
            }
            _figures.Objective(Objective);
            _figures.Units(_songs + _clock.Done, _songsWanted);
            int strums = _step == Step.Song && !clock ? _hits | (_beats << 12) : int.MinValue;
            if (strums != _cardStrums)
            {
                _cardStrums = strums;
                _figures.Limit(strums == int.MinValue ? null : JobText.T("reallife_job_busker_card_strums", "hits", _hits.ToString(), "beats", _beats.ToString()), _panel.White);
            }
            _figures.Money(_tips + _clockTips, 0);
            _figures.Time(Mathf.Max(0, _songsWanted - _songs - _clock.Done) * _clock.MinutesEach);
        }

        // ---------------------------------------------------------------- the rest of the set by the clock

        /// <summary>
        /// What a song usually brings in, for the clock to pay <see cref="ShiftRates.Share"/> of: the song's own formula (see
        /// <see cref="EndSong"/>) for the people about and the hour now, the middle song, and a player who hits
        /// <see cref="AveragePlay"/> of the strums.
        /// </summary>
        private float AverageTip()
        {
            float street = 0.5f + Mathf.Min(Crowd(), CrowdFull) / (float)CrowdFull;
            return Def.UnitPay * Ctx.Rate * street * HourFactor(TimeHelper.CurrentHour) * Worth[1] * (0.3f + AveragePlay) * Ctx.TipFactor * Ctx.Offer.CrowdFactor;
        }

        private void OfferSkip()
        {
            if (Ctx == null || Over || _step != Step.Pick || !_more || _songs >= _songsWanted || Time.time - _songOver < Debounce)
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
            // The street is cleared: whoever stopped to listen walks on, and the music stops until the player plays again.
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

        // The mod's clip and guitar if the bundle has them; else the game's own performance; with the game's music either way.
        private bool StartPlaying()
        {
            bool playing = Ctx.Self.Clip(StrumClip);
            if (!playing && _act != null && Ctx.Self.Pose(_act.animation))
            {
                playing = true;
                // Everything the game hangs on a performer of this act, and the first of the things it picks one of.
                Carry(_act.objectsData, false);
                Carry(_act.spawnOnlyOneObjectsData, true);
            }
            _playing = playing;
            if (playing)
            {
                Music(true);
            }
            return playing;
        }

        private void Carry(PerformerObjectData[] things, bool onlyOne)
        {
            ThirdPersonCharacter body = Ctx.Player.Character;
            for (int i = 0; things != null && i < things.Length; i++)
            {
                PerformerObjectData thing = things[i];
                if (thing == null || (!thing.isHandObject && !thing.isUpperChestObject))
                {
                    continue;   // stood on the ground beside a performer (a stool, an amplifier): not for someone who set up anywhere
                }
                Transform bone = thing.isUpperChestObject ? body.upperChest : thing.isRightHand ? body.rightHand : body.leftHand;
                if (Ctx.Self.GameProp(thing.objectName, bone, thing.position, Quaternion.Euler(thing.rotation)) && onlyOne)
                {
                    return;
                }
            }
        }

        private void Music(bool on)
        {
            try
            {
                if (!on)
                {
                    if (_music != null)
                    {
                        _music.Stop();
                        _music.clip = null;
                    }
                    return;
                }
                StreetPerformerData sound = _act ?? GameAct(PermanentAnimationType.PlayingGuitar);
                AudioClip clip = sound != null ? sound.clip : null;
                if (clip == null)
                {
                    return;
                }
                if (_music == null)
                {
                    // One source, kept on the jobs' own root for the session and reused.
                    Transform host = Ctx.Director.transform.Find("RealLifeOverhaul Busker Sound");
                    if (host == null)
                    {
                        host = new GameObject("RealLifeOverhaul Busker Sound").transform;
                        host.SetParent(Ctx.Director.transform, false);
                        host.gameObject.AddComponent<AudioSource>();
                    }
                    _music = host.GetComponent<AudioSource>();
                    _music.playOnAwake = false;
                    _music.loop = true;
                    _music.spatialBlend = 1f;
                    _music.minDistance = 4f;
                    _music.maxDistance = 40f;
                    _music.volume = 0.8f;
                    var references = InstanceBehavior<GlobalReferences>.Instance;
                    if (references != null)
                    {
                        _music.outputAudioMixerGroup = references.cityMixerGroup;
                    }
                }
                _music.transform.position = Ctx.Player.transform.position + Vector3.up * 1.2f;
                _music.clip = clip;
                _music.Play();
            }
            catch (Exception e)
            {
                Ctx.Logger?.Error(e);   // silent busking is still busking
            }
        }

        private void EndSong(float now)
        {
            _beatOpen = false;
            float quality = Mathf.Clamp01((_beats > 0 ? (float)_hits / _beats : 0f) - _wrong * 0.06f);
            int crowd = Crowd();
            float street = 0.5f + Mathf.Min(crowd, CrowdFull) / (float)CrowdFull;
            int tip = Mathf.RoundToInt(Def.UnitPay * Ctx.Rate * street * HourFactor(TimeHelper.CurrentHour) * Worth[_song] * (0.3f + quality) * Ctx.TipFactor * Ctx.Offer.CrowdFactor);   // a quiet street or a rush, as the offer said
            _tips += tip;
            _songs++;
            _handSongs++;
            UnitDone(_handSongs, UnitPay());   // the big tipper of the set, if the offer drew one
            _songOver = now;
            Ctx.Logger?.Info("Jobs: busking song " + _songs + ": " + _hits + " of " + _beats + " strums, " + _wrong + " wrong notes, " + crowd
                             + " people about, hour " + TimeHelper.CurrentHour + ": " + tip + " in the case.");
            JobText.Toast(UI.Notification.NotificationType.Info, JobText.Pick(quality >= 0.6f ? Good : Poor, ref _line), "amount", JobText.Money(tip), 6f);

            if (_songs >= _songsWanted)
            {
                Ctx.Say("reallife_job_busker_done", 7f);
                Conclude(false);
            }
            else if (_songs >= SongsBeforeMoveOn && Settings.Police && Random.value < MoveOnChance)
            {
                Ctx.Say("reallife_job_busker_moved_on", 9f);
                Conclude(false);
            }
            else if (SaveGameManager.Current.Energy < JobCatalogue.MinEnergy)
            {
                Ctx.Say("reallife_job_done_tired", 9f);
                Conclude(false);
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

        // ---------------------------------------------------------------- someone stops to listen

        private void Listener(float now)
        {
            if (_listener.Held)
            {
                if (_listener.PutAway)
                {
                    Unlook();
                    _listener.Forget();   // the game took them back meanwhile
                    return;
                }
                if (now < _listenUntil && _playing)
                {
                    return;
                }
                // Something in the case, and on their way.
                ThirdPersonCharacter body = _listener.Character;
                if (body != null)
                {
                    body.LookTarget = Vector3.zero;
                    Ctx.Cast.Emoji(body, CharacterEmojiName.CommonGreenHappy, 1.8f);
                    Vector3 from = body.transform.position;
                    if (!StreetSpots.AwayFrom(from, Ctx.PlayerPosition, 30f, false, out Vector3 to))
                    {
                        to = from;
                    }
                    _listener.HandBack(to);
                    if (_playing)
                    {
                        _tips += Mathf.Max(1, Mathf.RoundToInt(Random.Range(2.75f, 8.25f) * Ctx.Rate));
                    }
                }
                else
                {
                    _listener.Forget();
                }
                _nextListener = now + Random.Range(9f, 18f);
                return;
            }
            if (!_playing || _step != Step.Song || now < _nextListener)
            {
                return;
            }
            _nextListener = now + 3f;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            Vector3 here = Ctx.Player.transform.position;
            for (int i = 0; walkers != null && i < walkers.Count; i++)
            {
                Pedestrian walker = walkers[i];
                if (walker == null || !walker.isActiveAndEnabled || walker.tpc == null || !walker.tpc.visible || RagdollSystem.Holds(walker.tpc))
                {
                    continue;
                }
                Vector3 offset = walker.tpc.transform.position - here;
                // In front of the busker, a few steps off: not behind them, not on top of the case.
                if (offset.sqrMagnitude > ListenWithin * ListenWithin || offset.sqrMagnitude < 4f || Vector3.Dot(offset, _facing) < 1.2f
                    || offset.y > 1.5f || offset.y < -1.5f)
                {
                    continue;
                }
                if (!_listener.Take(walker))
                {
                    continue;
                }
                NavMeshAgent agent = walker.tpc.navmeshAgent;
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }
                agent.enabled = false;   // the game turns a character to its look target only while it is not being steered
                walker.tpc.LookTarget = here;
                _listenUntil = now + Random.Range(6f, 10f);
                return;
            }
        }

        // A pooled pedestrian keeps whatever look target it was left with: cleared before it is forgotten.
        private void Unlook()
        {
            ThirdPersonCharacter body = _listener.Character;
            if (body != null)
            {
                body.LookTarget = Vector3.zero;
            }
        }

        private void ReleaseListener()
        {
            if (!_listener.Held)
            {
                return;
            }
            ThirdPersonCharacter body = _listener.Character;
            if (body == null || _listener.PutAway)
            {
                Unlook();
                _listener.Forget();
                return;
            }
            body.LookTarget = Vector3.zero;
            Vector3 from = body.transform.position;
            if (Ctx == null || !StreetSpots.AwayFrom(from, Ctx.PlayerPosition, 30f, false, out Vector3 to))
            {
                to = from;
            }
            _listener.HandBack(to);
        }

        // ---------------------------------------------------------------- the end

        private void Conclude(bool badly)
        {
            Settle();
            Finish(badly ? JobEnd.Failed : JobEnd.Done);
        }

        private void Settle()
        {
            if (_paid || _songs <= 0 || Ctx == null || (_handSongs <= 0 && !(Ctx.Gig != null && _songs * 2 >= _songsWanted)))
            {
                return;
            }
            _paid = true;
            // A booked set: the venue's fee on top of the hat, for a set played at least half through (songs played by hand or left to the clock).
            // The clock's songs were paid as it ended, and earn no fee of their own.
            var slip = new PaySlip { Units = _handSongs, Tips = _tips, Bonus = Ctx.Gig != null && _songs * 2 >= _songsWanted ? Ctx.Gig.Fee : 0 };
            Ctx.Pay(this, slip, "reallife_job_busker_slip");
        }

        public override void End(JobEnd why)
        {
            if (Ctx == null)
            {
                return;
            }
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
