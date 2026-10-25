using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The construction objectives on the HUD: the player's construction matters under way (TrackRules), as a group of lines
    /// of its own in the game's objectives window (GameUi.TaskGroup), most urgent first, up to five and a line saying how
    /// many more. A press on a line opens the app on what it is about; its Set destination button shows it on the map; the
    /// group's headline folds it to its first line. Only the local player's own company and projects, written on this
    /// machine alone: nothing is sent or saved. COST: once a second, one flag looked at; the lines are written again only
    /// after the state changed (the director's Changed, each game hour's pass, a load) and the option is on.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private static bool _trackFolded;   // the player folded the group to its first line: kept for the session, a viewer's own choice

        private readonly List<TrackLine> _trackLines = new List<TrackLine>(16);
        private readonly List<string> _trackWords = new List<string>(TrackRules.Shown + 1);
        private readonly List<CellTone> _trackTones = new List<CellTone>(TrackRules.Shown + 1);
        private readonly List<bool> _trackMapped = new List<bool>(TrackRules.Shown + 1);
        private readonly Func<string, string> _trackText = ConstructionText.T;
        private readonly Func<int, string> _trackDay = ConstructionText.Day;
        private readonly Func<long, string> _trackMoney = ConstructionText.Money;
        private TaskGroup _track;
        private TrackFacts _trackFacts;
        private bool _trackDirty = true;
        private int _trackShown;

        /// <summary>The objectives' group, for the tests: null before it is first written.</summary>
        internal TaskGroup TrackGroup => _track;

        /// <summary>The lines active now, in order, for the tests and the group.</summary>
        internal IReadOnlyList<TrackLine> TrackLines => _trackLines;

        // Something the objectives show may have changed: written again on the next second's look.
        private void TrackChanged()
        {
            _trackDirty = true;
        }

        // Once a second while running: the group written again if anything changed, put away while the option is off.
        private void TrackSecond()
        {
            if (!Settings.ConstructionTrack)
            {
                if (_track != null && _track.Lines > 0)
                {
                    _track.Drop();
                }
                _trackDirty = true;   // written at once if it is switched back on
                return;
            }
            if (_trackDirty)
            {
                _trackDirty = false;
                TrackWrite();
            }
        }

        // The lines worked out from the state and written into the game's objectives window.
        private void TrackWrite()
        {
            _trackFacts = _trackFacts ?? new TrackFacts(this);
            int count = TrackRules.Lines(_state, _trackFacts, _trackLines);
            int shown = TrackRules.ShownOf(count, out int more);
            if (_trackFolded)
            {
                more += shown > 1 ? shown - 1 : 0;
                shown = Math.Min(shown, 1);
            }
            _trackWords.Clear();
            _trackTones.Clear();
            _trackMapped.Clear();
            for (int i = 0; i < shown; i++)
            {
                TrackLine line = _trackLines[i];
                _trackWords.Add(TrackRules.Words(line, _trackText, _trackDay, _trackMoney));
                _trackTones.Add(line.Need == TrackNeed.Urgent ? CellTone.Red : line.Need == TrackNeed.Yours ? CellTone.Yellow : CellTone.Plain);
                _trackMapped.Add(line.Project > 0 ? HasPlace(_state.Project(line.Project)) : line.Tender > 0 && HasPlace(_state.Tender(line.Tender)));
            }
            if (more > 0)
            {
                _trackWords.Add(ConstructionText.T("reallife_bld_track_more", "count", more.ToString()));
                _trackTones.Add(CellTone.Plain);
                _trackMapped.Add(false);
            }
            _trackShown = shown;
            if (_track == null)
            {
                _track = new TaskGroup(Logger, "construction objectives", "reallife_bld_track_header");
                _track.Pressed = TrackPressed;
                _track.Mapped = TrackMapped;
                _track.HeadPressed = TrackFold;
            }
            string head = _trackFolded && count > 1 ? ConstructionText.T("reallife_bld_track_header_folded", "count", count.ToString()) : ConstructionText.T("reallife_bld_track_header");
            _track.Show(head, _trackWords, _trackTones, _trackMapped);
            if (Verbose)
            {
                Logger?.Info("Construction check (objectives): " + count + " matter(s) under way, " + shown + " line(s) shown" + (more > 0 ? " and \"" + more + " more\"" : string.Empty)
                             + (_trackFolded ? ", folded" : string.Empty) + "; the group "
                             + (_track.Showing == count > 0 ? "shown only while something is under way: PASS." : "not as it should be: FAIL."));
            }
        }

        private void TrackPressed(int index)
        {
            if (index >= _trackShown || index >= _trackLines.Count)
            {
                Open(TabOverview, 0);   // "and N more": every project at a glance
                return;
            }
            TrackLine line = _trackLines[index];
            switch (line.Opens)
            {
                case TrackOpens.Tender: Open(TabTenders, line.Pick); break;
                case TrackOpens.Company: Open(TabCompany, 0); break;
                case TrackOpens.Crews: Open(TabCrews, 0); break;
                default: Open(TabProjects, line.Pick); break;
            }
        }

        private void TrackMapped(int index)
        {
            if (index >= _trackShown || index >= _trackLines.Count)
            {
                return;
            }
            TrackLine line = _trackLines[index];
            string why = line.Project > 0 ? ShowProject(_state.Project(line.Project)) : line.Tender > 0 ? ShowTender(_state.Tender(line.Tender)) : "reallife_bld_map_none";
            if (why != null)
            {
                ConstructionGame.Tell(UI.Notification.NotificationType.Info, why, null, null);
            }
        }

        private void TrackFold()
        {
            _trackFolded = !_trackFolded;
            TrackWrite();
        }

        private void TrackDown()
        {
            _track?.Drop();
            _trackDirty = true;
        }

        /// <summary>What the objectives read: the clock, the director's names, a site's share done, whether a project may start or be hired for.</summary>
        private sealed class TrackFacts : ITrackFacts
        {
            private readonly ConstructionDirector _d;

            public TrackFacts(ConstructionDirector d)
            {
                _d = d;
            }

            public int Day => ConstructionGame.Day;

            public int Hour => ConstructionGame.Hour;

            public string PlaceOf(ProjectFile p) => _d.PlaceWords(p);

            public string TenderOf(TenderFile t) => _d.TenderName(t);

            public string CrewOf(CrewFile c) => _d.ForemanWords(c);

            public string BuilderOf(int rival) => ConstructionText.T(BuildCatalogue.RivalKeys[BuildCatalogue.Clamp(rival, 0, BuildCatalogue.RivalCount - 1)]);

            public int Percent(ProjectFile p)
            {
                SiteFacts site = p.SiteId > 0 ? SiteOf(p.SiteId) : default(SiteFacts);
                return site.Known && site.Hours > 0f ? Mathf.Clamp(Mathf.RoundToInt(site.Done / site.Hours * 100f), 0, 100) : 0;
            }

            public bool CanStart(ProjectFile p) => _d.StartBlock(p) == null;

            public bool CanHire(ProjectFile p) => _d.HireBlock(p) == null;

            public int ClaimDue(int claim) => ConstructionDirector.ClaimDue(claim);
        }
    }
}
