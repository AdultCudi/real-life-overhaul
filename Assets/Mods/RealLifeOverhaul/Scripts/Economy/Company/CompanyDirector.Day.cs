using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The company's hour, day and save, and the one reading of the game they share.</summary>
    public sealed partial class CompanyDirector
    {
        // The businesses, yesterday's statements and the staff, read once, with the economy's figures.
        private void Read(int day, bool ranBefore, bool countLoss, out int added, out int dropped, out int gone)
        {
            _index = Prices.Level;
            _cycle = CityEconomy.State;
            CompanyGame.ReadShops(_state, _shops, _pool, day, ranBefore, out added, out dropped);
            _read++;
            CompanyGame.ReadBooks(_shops, day);
            for (int i = 0; countLoss && i < _shops.Count; i++)
            {
                ShopFile file = _shops[i].File;
                file.LossRun = _shops[i].HasBooks && _shops[i].Profit < 0 ? file.LossRun + 1 : 0;
            }
            ReadPeople(out gone);
        }

        private void ReadPeople(out int gone)
        {
            for (int i = 0; i < _shops.Count; i++)
            {
                _shops[i].Sick = 0;
            }
            CompanyGame.ReadStaff(_state, _shops, _index, _cycle, out gone);
            Recount();
        }

        /// <summary>Each business's staff counted from their files: how many, how many stand low, their standing summed.</summary>
        internal void Recount()
        {
            for (int i = 0; i < _shops.Count; i++)
            {
                _shops[i].Staff = _shops[i].Low = _shops[i].Standing = 0;
            }
            List<StaffFile> staff = _state.Staff;
            for (int i = 0; i < staff.Count; i++)
            {
                CompanyShop shop = ShopAt(staff[i].Place);
                if (shop != null)
                {
                    shop.Staff++;
                    shop.Standing += staff[i].Standing;
                    shop.Low += StaffRules.StandsLow(staff[i]) ? 1 : 0;
                }
            }
        }

        private static readonly int HotOnHour = HotSpots.Slot("Economy: CompanyDirector.OnHour");
        // The game raises its hour event every hour and first thing in NewDay, before its daily run.
        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotOnHour);
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                _holding = false;
                int day = Save.Day;
                if (day > _state.LastDay)
                {
                    CompanyGame.NoteHours(_state);   // before the game's run pays and clears them
                    return;                          // the day event writes every figure in a moment
                }
                for (int i = 0; i < _shops.Count; i++)
                {
                    int asked = Footfall(_shops[i], day);
                    if (asked != 0 || _shops[i].Applied)
                    {
                        WriteFootfall(_shops[i], asked, false, false);
                    }
                }
                if (On(CompanyPart.Staff))
                {
                    try
                    {
                        StaffHour(day, Save.Hour);
                    }
                    catch (Exception e)
                    {
                        PartFault(CompanyPart.Staff, "the hour", e);
                    }
                }
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        private static readonly int HotOnDay = HotSpots.Slot("Economy: CompanyDirector.OnDay");
        // The game's day event comes after its daily run; the credit side and the city's economy have done their day.
        private void OnDay()
        {
            using var hotSpot = HotSpots.Time(HotOnDay);
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                int day = Save.Day;
                if (day <= _state.LastDay)
                {
                    return;
                }
                float started = Time.realtimeSinceStartup;
                _holding = false;
                bool next = day == _state.LastDay + 1;   // a jump of many days is one pass, for today
                _state.LastDay = day;
                _state.Age++;
                int added, dropped, gone;
                Read(day, false, next, out added, out dropped, out gone);
                float read = Time.realtimeSinceStartup;
                for (int p = 0; p < (int)CompanyPart.Count; p++)
                {
                    var part = (CompanyPart)p;
                    if (!On(part))
                    {
                        continue;
                    }
                    try
                    {
                        PartDay(part, day);
                    }
                    catch (Exception e)
                    {
                        PartFault(part, "the day", e);
                    }
                }
                float ruled = Time.realtimeSinceStartup;
                int written = WriteAll(day, true, true);
                RotaRead();
                Changed();
                if (Verbose)
                {
                    float done = Time.realtimeSinceStartup;
                    Logger?.Info("Company check (day " + day + "): " + _shops.Count + " business(es)" + (added > 0 ? ", " + added + " new" : string.Empty)
                                 + (dropped > 0 ? ", " + dropped + " no longer the player's" : string.Empty)
                                 + ", " + _state.Staff.Count + " employee(s)" + (gone > 0 ? ", " + gone + " gone and their files dropped" : string.Empty)
                                 + ", prices " + (_index / 10f).ToString("0.0", CultureInfo.InvariantCulture) + ", the economy " + _cycle
                                 + ", " + written + " footfall figure(s) written, " + _alerts.Count + " row(s) for the Planner; reading the game "
                                 + ((read - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms, the rules "
                                 + ((ruled - read) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms, footfall and rows "
                                 + ((done - ruled) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms, "
                                 + ((done - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms in all: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        // The registration that holds a footfall figure is saved, so the figures are the game's own until the next hour.
        private void OnSave()
        {
            try
            {
                if (!_running || Save == null || _state == null)
                {
                    return;
                }
                int put = 0;
                _holding = true;
                for (int i = 0; i < _shops.Count; i++)
                {
                    if (_shops[i].Applied)
                    {
                        WriteFootfall(_shops[i], 0, false, false);
                        put++;
                    }
                }
                Write(Save);
                if (Verbose)
                {
                    Logger?.Info("Company check (before a save): " + put
                                 + " footfall figure(s) put at the game's own until the next game hour; the state written under its own key: PASS");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Write(GameInstance save)
        {
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            save.modData[CompanyStore.Key] = CompanyStore.Write(_state);
            RotaSave(save);
            if (_beforeTests != null)
            {
                save.modData[CompanyStore.TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(CompanyStore.TestKey);
            }
        }
    }
}
