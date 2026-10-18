using System;
using UI.Elements;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>What a sitting is for; never saved.</summary>
    public enum SittingKind
    {
        Class = 0,      // a class of a course, at its campus or online at home
        Homework = 1,   // a piece of homework or revision, at home or at the campus
        Exam = 2,       // a course's exam
        Work = 3,       // a day at a job
        Overtime = 4,   // more hours of it
        Contract = 5,   // freelance work or a practice's client
        Interview = 6,  // a job interview
        Tutoring = 7,   // a tutoring session given
        Ceremony = 8    // a graduation day
    }

    /// <summary>A class, homework, an exam or a working day left to the game's clock on its activity panel; the director is told its minutes, or that it was called off.</summary>
    public sealed class Sitting : ISkippableShift
    {
        private readonly CareersDirector _director;
        private readonly string _title;
        private readonly string[] _lines;
        private readonly bool _tiring;
        private int _minutes;
        private bool _running;

        public readonly SittingKind Kind;
        public readonly int Of;          // the course's id, the job's, the contract's
        public readonly int Length;      // game minutes asked for

        public Sitting(CareersDirector director, SittingKind kind, int of, int minutes, string title, bool tiring, params string[] lines)
        {
            _director = director;
            Kind = kind;
            Of = of;
            Length = Math.Max(1, minutes);
            _minutes = Length;
            _title = title;
            _tiring = tiring;
            _lines = lines ?? new string[0];
        }

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
            _running = true;
            if (_tiring)
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
            if (_running && _tiring)
            {
                CareersGame.Tiring(false);
            }
            _running = false;
            _minutes = 0;
            _director.Ended(this, minutes, whole);
        }

        /// <summary>Called off before it began (the walk to the desk or the computer stopped): nothing is counted and nothing is said.</summary>
        public void CalledOff()
        {
            if (_running && _tiring)
            {
                CareersGame.Tiring(false);
            }
            _running = false;
            _minutes = 0;
            _director.Dropped(this);
        }
    }
}
