using System;
using System.Collections;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.Economy;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.DaySeries
{
    /// <summary>
    /// Writes the day series once a day, after the economy's day, and once when a save is first seen. No Update, so nothing
    /// per frame: the timer is one comparison a second, and the write itself is the day's pass.
    /// </summary>
    public sealed class DaySeriesDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static DaySeriesDirector Instance { get; private set; }

        private static readonly int HotTick = HotSpots.Slot("Day series: DaySeriesDirector.Tick");

        private readonly WaitForSecondsRealtime _wait = new WaitForSecondsRealtime(1f);
        private DaySeriesBook _book = new DaySeriesBook();
        private Action _onSave, _onDay;
        private GameInstance _loadedFor;
        private int _written = -1;
        private bool _broken;

        /// <summary>The save's series, or null before the director exists.</summary>
        public static DaySeriesBook Book => Instance != null ? Instance._book : null;

        /// <summary>Bumped when a figure is written, so a chart rebuilds then.</summary>
        public static int Generation => Book != null ? Book.Generation : 0;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _onDay = OnDay;
            EconomyDirector.AfterDay += _onDay;
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            EconomyDirector.AfterDay -= _onDay;
            SaveHook.Drop(_onSave);
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Tick()
        {
            while (true)
            {
                yield return _wait;
                if (_broken)
                {
                    continue;
                }
                using var hotSpot = HotSpots.Time(HotTick);
                GameInstance save = SaveGameManager.Current;
                if (save == null || LoadScene.isLoading || !ModState.Loaded)
                {
                    continue;
                }
                if (!ReferenceEquals(_loadedFor, save))
                {
                    Load(save);
                }
                SaveHook.Keep(_onSave);
                if (_written != save.Day)
                {
                    Record(save.Day);
                }
            }
        }

        private void OnDay()
        {
            GameInstance save = SaveGameManager.Current;
            if (_broken || save == null || !ReferenceEquals(_loadedFor, save) || _written == save.Day)
            {
                return;
            }
            try
            {
                Record(save.Day);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void Load(GameInstance save)
        {
            if (save.modData == null)
            {
                save.modData = new System.Collections.Generic.Dictionary<string, string>();
            }
            DaySeriesBook book = DaySeriesStore.Load(save.modData, out int version);
            _book = book ?? new DaySeriesBook();
            if (_book.Seed == 0)
            {
                _book.Seed = DaySeriesRules.DefaultSeed;
            }
            _loadedFor = save;
            _written = -1;
            Logger?.Info("Day series: " + (book != null ? "loaded version " + version + ", " + _book.Lines + " figures to day " + _book.LastDay : "none in this save, it starts today") + ".");
        }

        private void Record(int day)
        {
            try
            {
                DaySeriesGame.Record(_book, day);
                _written = day;
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void OnSave()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null || save.modData == null || !ReferenceEquals(_loadedFor, save) || _broken)
                {
                    return;
                }
                if (SaveLines.IsKept(save.modData, DaySeriesCatalogue.Key))
                {
                    return;   // a line this version cannot read stays as it was
                }
                save.modData[DaySeriesCatalogue.Key] = DaySeriesStore.Write(_book);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void Fault(Exception e)
        {
            _broken = true;
            Logger?.Warn("Day series stopped for this session: " + e.GetType().Name);
        }
    }
}
