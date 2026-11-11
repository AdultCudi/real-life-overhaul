using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>The rival tycoons' lines and the city's estates: the same rules as the founder's, a day at a time, and the pieces the paper prints of them.</summary>
    public sealed partial class DynastyDirector
    {
        // Each tycoon who is forward has an age; on the day they retire or die their fortune pays the estate tax by the founder's own bands and their heir takes the holding.
        // A tycoon who is not forward, is out, or rests (a Going Public session) is left alone: RivalWorth answers -1.
        private void RivalsDay(int day, int year)
        {
            if (DynastyBridges.RivalWorth == null)
            {
                return;
            }
            for (int i = 0; i < _state.RivalLines.Length; i++)
            {
                long worth = DynastyBridges.RivalWorth(i);
                if (worth < 0)
                {
                    continue;
                }
                RivalLine r = _state.RivalLines[i];
                DynastyRivals.Begin(r, _state.Seed, i, day, year);
                RivalHands(i, r, DynastyRivals.Due(r, day, year), worth, day, year);
            }
        }

        private void RivalHands(int tycoon, RivalLine r, RivalEvent due, long worth, int day, int year)
        {
            if (due == RivalEvent.None)
            {
                return;
            }
            string name = DynastyBridges.RivalName != null ? DynastyBridges.RivalName(tycoon) : string.Empty;
            if (due == RivalEvent.ElderDied)
            {
                DynastyRivals.ElderGone(r);
                Note(new DynastyNote { Day = day, Who = tycoon, Mode = HandMode.Elder, Generation = r.Generation, From = name });
                return;
            }
            long tax = DynastyRivals.TaxOf(worth, DynastyGame.Level);
            long paid = DynastyBridges.RivalTax != null ? Math.Max(0L, DynastyBridges.RivalTax(tycoon, tax)) : 0L;
            DynastyRivals.HandOver(r, due, _state.Seed, tycoon, day, year, worth, paid);
            DynastyBridges.RivalSlow?.Invoke(tycoon, day + DynastyCatalogue.RivalSlowDays);
            Note(new DynastyNote { Day = day, Who = tycoon, Mode = r.HandMode, Generation = r.Generation, Tax = paid, From = name });
            Logger?.Info("Dynasty: " + name + (due == RivalEvent.Retired ? " retired" : " died") + " on day " + day + ": the fortune of " + worth + " bore " + tax + " of estate tax, of which "
                         + paid + " left it (the rest would have taken it under its floor); generation " + r.Generation + " leads the holding.");
            if (Verbose)
            {
                Logger?.Info("Dynasty check (a rival's handover): tax by the founder's bands " + tax + ", paid " + paid + " of a fortune of " + worth + ": "
                             + (paid >= 0 && paid <= tax && paid <= worth ? "PASS" : "FAIL"));
            }
        }

        private void Note(DynastyNote n)
        {
            _state.Notes.Add(n);
            while (_state.Notes.Count > DynastyCatalogue.NotesKept)
            {
                _state.Notes.RemoveAt(0);
            }
        }

        /// <summary>A household of the city ended with a death (the population's own day tells it): its estate is counted by the game's year.</summary>
        public void HouseholdEnded(long savings, long debt)
        {
            if (!Running)
            {
                return;
            }
            DynastyCity.Turn(_state.City, DynastyGame.Day / DynastyGame.DaysPerYear);
            DynastyCity.Ended(_state.City, savings, debt, DynastyGame.Level);
        }

        /// <summary>The line the rich list and a tycoon's panel add: the generation that leads the holding and what the last handover cost; empty before one.</summary>
        public string RivalLineOf(int tycoon)
        {
            if (!Running || tycoon < 0 || tycoon >= _state.RivalLines.Length || !_state.RivalLines[tycoon].Ready)
            {
                return string.Empty;
            }
            return DynastyText.RivalLine(tycoon, _state.RivalLines[tycoon], DynastyGame.Day, DynastyGame.DaysPerYear);
        }

        /// <summary>The morning's pieces for the City Ledger: yesterday's handovers, the family's and the rivals', each once.</summary>
        public void Stories(int day, List<DynastyStory> into)
        {
            into.Clear();
            if (!Running)
            {
                return;
            }
            for (int i = 0; i < _state.Notes.Count; i++)
            {
                DynastyNote n = _state.Notes[i];
                if (n.Day != day - 1)
                {
                    continue;   // a handover is printed the morning after it: nothing twice
                }
                into.Add(DynastyText.Story(n, n.Tax >= DynastyRules.At(DynastyCatalogue.PaperFrom, DynastyGame.Level)));
            }
        }
    }
}
