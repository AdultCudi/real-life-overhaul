using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The director's part for the market's big moments (LedgerStories, LedgerLeads): a rival tycoon taken over, a company's
    /// milestones, a new most valuable company and a sector's lead changing hands, read when an edition is first printed and
    /// kept when it is written again; a guest following the host's paper keeps its record in step and sends its own rival's
    /// story to the host. The cost: nothing per frame; two passes over the listed companies a morning.
    /// </summary>
    public sealed partial class LedgerDirector
    {
        private static readonly Func<double, string> Large = LedgerText.Large;

        private readonly List<LedgerCompany> _companies = new List<LedgerCompany>(LedgerStories.MarksMost / 2);
        private readonly List<LedgerBeat> _beats = new List<LedgerBeat>(4);
        private readonly List<LedgerOpening> _openings = new List<LedgerOpening>(4);
        private readonly List<LedgerPiece> _testPieces = new List<LedgerPiece>(2);
        private LedgerStoryCount _count;

        // An edition's stories: read from the rivals and the market on its first print (the record moves on once), kept from
        // the edition it replaces when it is written again, the test's on top; the rivals' one line goes where a story covers it.
        private void StoriesInto(LedgerEdition e, int since, int day, bool fresh)
        {
            _count = default(LedgerStoryCount);
            OpenedInto(e, since, day, fresh);
            if (!Settings.LedgerStories)
            {
                if (_state.MarksKept || _state.LeadsKept)
                {
                    LedgerStories.Forget(_state);   // switched on again, the market is taken as it stands then
                }
                _testPieces.Clear();
                return;
            }
            if (fresh)
            {
                BeatenInto(e.Pieces, since, day);
                if (CompaniesRead())
                {
                    LedgerLeads.Pass(_state, _companies, false, Large, LedgerBridges.SectorName, e.Pieces, ref _count);   // first: it asks the milestones' record who was on the market
                    LedgerStories.Marks(_state, _companies, false, Large, e.Pieces, ref _count);
                }
            }
            else if (_state.Edition != null)
            {
                for (int i = 0; i < _state.Edition.Pieces.Count; i++)
                {
                    LedgerPiece kept = _state.Edition.Pieces[i];
                    if (kept != null && LedgerStories.Kept(kept.Key))
                    {
                        LedgerStories.AddOnce(e.Pieces, kept);
                    }
                }
            }
            for (int i = 0; i < _testPieces.Count; i++)
            {
                LedgerStories.AddOnce(e.Pieces, _testPieces[i]);
            }
            _testPieces.Clear();
            LedgerStories.Covered(e.Pieces);
        }

        // The businesses that opened since the edition before, each once: read from the city's record on the edition's first print (the window runs from the
        // day of the last edition up to, not including, today, so a business is in the edition after its day and in no other), kept when it is written again.
        private void OpenedInto(LedgerEdition e, int since, int day, bool fresh)
        {
            if (fresh)
            {
                if (LedgerBridges.Opened == null)
                {
                    return;
                }
                _openings.Clear();
                LedgerBridges.Opened(since, day, _openings);
                for (int i = 0; i < _openings.Count; i++)
                {
                    LedgerStories.AddOnce(e.Pieces, LedgerStories.Opened(_openings[i]));
                }
                return;
            }
            if (_state.Edition == null)
            {
                return;
            }
            for (int i = 0; i < _state.Edition.Pieces.Count; i++)
            {
                LedgerPiece kept = _state.Edition.Pieces[i];
                if (kept != null && LedgerStories.OpenedStory(kept.Key))
                {
                    LedgerStories.AddOnce(e.Pieces, kept);
                }
            }
        }

        private void BeatenInto(List<LedgerPiece> into, int since, int day)
        {
            if (LedgerBridges.Beaten == null)
            {
                return;
            }
            _beats.Clear();
            LedgerBridges.Beaten(since, day, _beats);
            for (int i = 0; i < _beats.Count; i++)
            {
                if (LedgerStories.AddOnce(into, LedgerStories.Beaten(_beats[i], Large)))
                {
                    _count.Beaten++;
                }
            }
        }

        private bool CompaniesRead()
        {
            _companies.Clear();
            return LedgerBridges.Companies != null && LedgerBridges.Companies(_companies);
        }

        // A guest following the host's paper, once a morning: its record follows the shared market without a story (the host's
        // paper has them), and its own rival taken over yesterday goes with its pieces to the host (the rivals are its own).
        private void StoriesGuest(int day, List<LedgerPiece> into)
        {
            if (!Settings.LedgerStories)
            {
                if (_state.MarksKept || _state.LeadsKept)
                {
                    LedgerStories.Forget(_state);
                }
                return;
            }
            _count = default(LedgerStoryCount);
            BeatenInto(into, day - 1, day);
            if (CompaniesRead())
            {
                LedgerLeads.Pass(_state, _companies, true, Large, LedgerBridges.SectorName, into, ref _count);
                LedgerStories.Marks(_state, _companies, true, Large, into, ref _count);
            }
        }

        // The test's three stories, ready for the next print: a rival's takeover (the wiring's made-up facts), a milestone and a
        // sector's lead changing hands. Returns the rival's piece; the milestone's, whose it is and its company's name come out,
        // and the lead's with the company that takes it.
        private LedgerPiece TestStoriesReady(int day, out LedgerPiece mark, out bool own, out string name, out LedgerPiece lead, out string leadName)
        {
            _testPieces.Clear();
            LedgerPiece rival = LedgerStories.Beaten(LedgerBridges.TestBeat != null ? LedgerBridges.TestBeat(day) : null, Large);
            if (rival != null)
            {
                _testPieces.Add(rival);
            }
            own = false;
            name = null;
            leadName = null;
            bool market = CompaniesRead();
            mark = market ? LedgerStories.TestMark(_companies, Large, out own, out name) : null;
            if (mark != null)
            {
                _testPieces.Add(mark);
            }
            lead = market ? LedgerLeads.Test(_companies, Large, LedgerBridges.SectorName, out leadName) : null;
            if (lead != null)
            {
                _testPieces.Add(lead);
            }
            return rival;
        }

        // The self-check, once an edition: the stories found against those the edition kept under its cap.
        private void StoriesCheck(LedgerEdition e, int found)
        {
            if (!Meta.Dev.Verbose || !Settings.LedgerStories)
            {
                return;
            }
            int printed = LedgerStories.Count(e.Pieces);
            int bought = found - _count.Beaten - _count.Milestones - _count.Top - _count.Leads;
            Logger?.Info("City Ledger check (milestones and rivals): " + printed + " of " + found + " stories printed in the edition of day " + e.Day + " (read this morning: "
                         + _count.Beaten + " rival(s) taken over, " + _count.Milestones + " milestone(s), " + _count.Top + " new most valuable, " + _count.Leads + " new sector leader(s); "
                         + Math.Max(0, bought) + " kept, tested or a takeover by a listed company"
                         + (_count.Seeded > 0 ? "; " + _count.Seeded + " companies set where they stand without a story, the record being new" : string.Empty) + "; "
                         + _state.Marks.Count + " companies and " + _state.Leads.Count + " sector leaders in the record): "
                         + (printed == found ? "PASS"
                             : e.Pieces.Count >= LedgerCatalogue.PiecesMost ? (found - printed) + " of the lightest cut, the edition being full at " + LedgerCatalogue.PiecesMost + " pieces"
                             : "FAIL, " + (found - printed) + " lost with room left"));
        }
    }
}
