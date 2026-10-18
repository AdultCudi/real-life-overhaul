using System;
using PlayerActivity;
using UI.Elements;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A class, homework, an exam or a working day left to the game's clock on its activity panel; the director is told its
    /// minutes, or that it was called off. Its own <see cref="Record"/> says what it is for and is settled once, so that a
    /// second end, or an end after the director has let it go, counts nothing.
    /// </summary>
    public sealed class Sitting : ISkippableShift
    {
        private readonly CareersDirector _director;
        private readonly string _title;
        private readonly string[] _lines;
        private int _minutes;
        private bool _running;

        public readonly SittingKind Kind;
        public readonly int Of;          // the course's id, the job's, the contract's
        public readonly int Length;      // game minutes asked for
        public readonly SittingRecord Record;

        public Sitting(CareersDirector director, SittingKind kind, int of, int minutes, string title, bool tiring, params string[] lines)
        {
            _director = director;
            Kind = kind;
            Of = of;
            Length = Math.Max(1, minutes);
            _minutes = Length;
            _title = title;
            _lines = lines ?? new string[0];
            Record = new SittingRecord(kind, of, Length, tiring);
        }

        /// <summary>The activity on the game's panel that runs it, once the director has handed it out; null before.</summary>
        internal IPlayerActivity Holder => Record.Holder as IPlayerActivity;

        public string SkipTitle => _title;

        public int SkipMinutes => _minutes;

        /// <summary>Its lines as written, for a desk's panel before it is begun (SchoolDesk.DeskOffer.Lines). The array itself, not a copy.</summary>
        public string[] Lines => _lines;

        public LabelInfo[] SkipLabels()
        {
            Color32 white = GameUi.WorldPanel.White();
            var labels = new LabelInfo[_lines.Length];
            for (int i = 0; i < _lines.Length; i++)
            {
                labels[i] = new LabelInfo(_lines[i] ?? string.Empty, white, false);
            }
            return labels;
        }

        /// <summary>Whole money charged as it begins (a retake's fee at a desk), so that a walk called off costs nothing; 0 for none.</summary>
        public int Fee;

        public void SkipStarted()
        {
            if (!Record.Begin())
            {
                return;   // let go already: it never begins
            }
            _running = true;
            if (Record.Tiring)
            {
                CareersGame.Tiring(true);
            }
            if (Fee > 0)
            {
                int fee = Fee;
                Fee = 0;   // once
                _director.ChargeAtStart(this, fee);
            }
        }

        public void SkipWorked(int minutes)
        {
        }

        public void SkipEnded(int minutes, bool whole)
        {
            if (!Record.Settle(SittingEnd.Ended))
            {
                return;   // settled already: counted once
            }
            Rest();
            _director.Ended(this, minutes, whole);
        }

        /// <summary>Called off before it began (the walk to the desk or the computer stopped): nothing is counted and nothing is said.</summary>
        public void CalledOff()
        {
            if (!Record.Settle(SittingEnd.CalledOff))
            {
                return;
            }
            Rest();
            _director.Dropped(this);
        }

        /// <summary>Let go by the director with nothing counted (its activity gone unsettled, or the part stopping); false if it was settled already.</summary>
        internal bool LetGo()
        {
            if (!Record.Settle(SittingEnd.LetGo))
            {
                return false;
            }
            Rest();
            return true;
        }

        // Over, whichever way: its energy spent no more, nothing left to pass.
        private void Rest()
        {
            if (_running && Record.Tiring)
            {
                CareersGame.Tiring(false);
            }
            _running = false;
            _minutes = 0;
        }
    }
}
