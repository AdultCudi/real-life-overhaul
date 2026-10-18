using System;
using UI.Elements;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>What an hour or more left to the clock is for: the saved number is never kept.</summary>
    public enum SittingKind
    {
        Class = 0,      // a class of a course, at its campus or online at home
        Homework = 1,   // a piece of homework or revision, at home or at the campus
        Exam = 2,       // a course's exam
        Work = 3,       // a day at a job (Stage B)
        Overtime = 4,   // more hours of it
        Contract = 5,   // freelance work or a practice's client (Stage D)
        Interview = 6,  // a job interview (Stage B)
        Tutoring = 7,   // a tutoring session given (Stage F)
        Ceremony = 8    // a graduation day (Stage E)
    }

    /// <summary>
    /// A class, a piece of homework, an exam or a day at work, left to the game's own clock as the game's own job and study
    /// are: on the game's activity panel with its progress bar, fast-forward and time skip, either where the player stands
    /// (<see cref="ShiftSkip"/>, where nothing in the world fits) or at the desk or computer it is done at, walked to first
    /// (Careers/SchoolDesk, Careers/ComputerSitting, Careers/DeskWork). Energy is spent as the game's own studying spends it
    /// while it runs; whichever way it ends, the director is told the minutes that passed (or, called off before it
    /// began, that it was: <see cref="CalledOff"/>).
    /// </summary>
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
                Fee = 0;   // (once)
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
