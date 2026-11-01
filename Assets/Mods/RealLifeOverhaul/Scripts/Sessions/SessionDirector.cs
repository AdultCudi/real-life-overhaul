using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Sessions
{
    /// <summary>
    /// The desk sessions (DRAFT2 section 3, "Desks"): every hands-on management session of the living economy is held at
    /// the owner's own desk in the firm's office, on the game's activity panel, or by a manager the player hired, who
    /// decides by their skill on the session's day and is heard of by a notice. The kinds come from the features through
    /// <see cref="SessionRegistry"/>; this keeps when each was held, who takes it and what it left (<see cref="SessionStore"/>).
    /// Under the master option Living population and its own switch: off, no desk is clickable, nobody is reminded and no
    /// effect is read. The cost: no Update; a one-second timer on the real clock comparing the day and hour; an hour's turn
    /// reads the player's buildings once; in an office of the player's, the owner's desk is looked for on going in and when
    /// the building's items change in number, a pass over its items.
    /// </summary>
    public sealed partial class SessionDirector : MonoBehaviour
    {
        private static readonly int HotTick = HotSpots.Slot("Sessions: SessionDirector.Tick");

        public static IModLogger Logger;
        public static SessionDirector Instance { get; private set; }

        private SessionFile _file;
        private GameInstance _save;
        private Action _onSave;
        private bool _broken;
        private int _day = -1, _hour = -1;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        /// <summary>True while sessions run: the master option and the sessions' own on, and no fault has stopped them.</summary>
        public static bool On => Settings.LivingPopulation && Settings.LivingDesks && Instance != null && !Instance._broken;

        /// <summary>The effect a kind's last holding left at a business while it lasts (SessionFile.Effect); none while sessions are off.</summary>
        public static bool Effect(string place, string session, int day, out int a, out int b)
        {
            a = b = 0;
            SessionDirector d = Instance;
            return On && d._file != null && d._file.Effect(place, session, day, out a, out b);
        }

        /// <summary>
        /// A kind's record at a business, for a page that says when it is due and who takes it: the day it was last held
        /// (SessionRules.Never for never) and whether a manager takes it. False while sessions are off.
        /// </summary>
        public static bool Record(string place, string session, out int held, out bool delegated)
        {
            held = SessionRules.Never;
            delegated = false;
            SessionDirector d = Instance;
            if (!On || d._file == null)
            {
                return false;
            }
            SessionRecord r = d._file.Find(place, session);
            if (r != null)
            {
                held = r.Held;
                delegated = r.Delegated;
            }
            return true;
        }

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            SessionGame.Clicks = DeskClicked;
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                DeskRelease("the mod unloading");
                SessionGame.Remove();
                SessionGame.Clicks = null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Tick()
        {
            var second = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return second;
                if (_broken)
                {
                    continue;
                }
                try
                {
                    Slow();
                }
                catch (Exception e) when (Guard.StandsDown(e, "the desk sessions"))
                {
                    Stop();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    Logger?.Warn("Sessions: the desk sessions are off for this session after the error above; what they kept is saved as it was.");
                    Stop();
                }
            }
        }

        // A fault: nothing more is run, the desk is let go, and the save keeps the line as last written.
        private void Stop()
        {
            _broken = true;
            DeskRelease("a fault");
        }

        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            GameInstance save = Save;
            if (save == null || LoadScene.isLoading || GameManager.isCitySceneBeingUnloaded || Meta.SaveTools.Dormant || !ModState.Loaded)
            {
                DeskRelease("no city");
                return;
            }
            if (!ReferenceEquals(save, _save))
            {
                DeskRelease("another save");
                _save = save;
                Load(save);
            }
            SaveHook.Keep(_onSave);   // the line is written while off too, so switching off keeps what was held
            if (!On)
            {
                DeskRelease("the option is off");
                return;
            }
            int day = save.Day, hour = SessionGame.Hour;
            if (day != _day || hour != _hour)
            {
                _day = day;
                _hour = hour;
                HourTurn(day, hour);
            }
            DeskWatch();
        }

        private void Load(GameInstance save)
        {
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            SessionFile f = SessionStore.Load(save.modData, out int version);
            if (version < 0)
            {
                SaveLines.Unreadable(save.modData, SessionStore.Key, "it could not be read");   // put back as it was read at every save
                Logger?.Warn("Sessions: the save's line of desk sessions could not be read (" + SessionStore.Key + "); every session starts as never held, and the line is kept in the save as it is.");
            }
            bool fresh = f == null;
            _file = f ?? new SessionFile { Seed = new System.Random().Next(1, int.MaxValue) };   // a save's own seed for its managers' rolls, kept with it
            _day = _hour = -1;
            if (!fresh || Verbose)
            {
                Logger?.Info("Sessions: " + (fresh ? "opened for this save" : "loaded (version " + version + ")") + ": " + _file.Records.Count + " record(s), "
                             + SessionRegistry.All.Count + " kind(s) of session.");
            }
        }

        // Inside the game's save event: the line, unless nothing was ever held and the save had none.
        private void OnSave()
        {
            GameInstance save = Save;
            if (save == null || save.modData == null || _file == null || !ReferenceEquals(save, _save) || SaveLines.IsKept(save.modData, SessionStore.Key))
            {
                return;
            }
            if (_file.Records.Count == 0 && !save.modData.ContainsKey(SessionStore.Key))
            {
                return;
            }
            save.modData[SessionStore.Key] = SessionStore.Write(_file);
        }

        /// <summary>Everything of the sessions in the world let go (the options' clean-up): the panel closed, nothing held, the desk's click off.</summary>
        public void Release()
        {
            DeskRelease("the clean-up");
        }

        /// <summary>The feature's data is reset (Meta/SaveTools): what the save kept is forgotten.</summary>
        public void Forget()
        {
            DeskRelease("reset");
            _file = new SessionFile { Seed = new System.Random().Next(1, int.MaxValue) };
            _day = _hour = -1;
            GameInstance save = Save;
            save?.modData?.Remove(SessionStore.Key);
        }
    }
}
