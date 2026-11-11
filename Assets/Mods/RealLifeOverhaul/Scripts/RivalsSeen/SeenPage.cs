using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// The lines the rivals' pages ask for, made only when something they say has changed: the Rivals app's chosen rival (the car they drive and where they are
    /// usually found, in a few words) and a tycoon's panel line (SeenLines.Tycoon). Each answer is kept with the file's revision it was made at, the director's
    /// count of every change to what a page could say (a day's pass, an hour's, a greeting, a load, a test, the language), and the day and hour for the line that
    /// says where somebody will be next; asked again with nothing changed, the kept answer is given and nothing is made. Game-free.
    /// COST: a walk of the day's records to find the one asked for, once per change; nothing at all otherwise.
    /// </summary>
    public sealed class SeenPage
    {
        private string _owner;               // the game's id of the owner last asked for
        private int _ownerRevision = -1;
        private string _ownerCar, _ownerFound;
        private int _tycoon = -1, _tycoonRevision = -1, _tycoonDay = -1, _tycoonHour = -1;
        private string _tycoonLine = string.Empty;

        /// <summary>Answers made, as against given from what was kept: the checks count them.</summary>
        public int Made { get; private set; }

        /// <summary>Nothing kept: the next question makes its answer.</summary>
        public void Forget()
        {
            _owner = null;
            _ownerRevision = -1;
            _ownerCar = _ownerFound = null;
            _tycoon = _tycoonRevision = _tycoonDay = _tycoonHour = -1;
            _tycoonLine = string.Empty;
        }

        /// <summary>
        /// What the Rivals app says of one of the game's rival owners, by the game's own id: the car they drive (null with none on record) and where they are
        /// usually found. False, both null, for somebody not on record, never marked or gone.
        /// </summary>
        public bool Owner(SeenFile f, IList<SeenSubject> subjects, string gameId, int revision, ISeenWords w, out string car, out string found)
        {
            if (revision != _ownerRevision || !string.Equals(gameId, _owner, StringComparison.Ordinal))
            {
                _owner = gameId;
                _ownerRevision = revision;
                _ownerCar = _ownerFound = null;
                Made++;
                SeenRecord r = OwnerRecord(f, gameId);
                SeenSubject s = r != null ? Subject(subjects, r.Key) : null;
                if (s != null && !s.Gone && r.MarkDay >= 0 && w != null)
                {
                    _ownerCar = r.Car.Length > 0 ? w.Car(r.Car) : null;
                    _ownerFound = SeenLines.Found(r.Form, w);
                }
            }
            car = _ownerCar;
            found = _ownerFound;
            return found != null;
        }

        /// <summary>A tycoon's line for their panel and the rich list, by their place on it; empty with nothing to say.</summary>
        public string Tycoon(SeenFile f, IList<SeenSubject> subjects, int index, int day, int hour, int revision, ISeenWords w)
        {
            if (index != _tycoon || revision != _tycoonRevision || day != _tycoonDay || hour != _tycoonHour)
            {
                _tycoon = index;
                _tycoonRevision = revision;
                _tycoonDay = day;
                _tycoonHour = hour;
                Made++;
                SeenRecord r = TycoonRecord(f, index);
                _tycoonLine = r != null ? SeenLines.Tycoon(f, r, Subject(subjects, r.Key), day, hour, w) : string.Empty;
            }
            return _tycoonLine;
        }

        private static SeenRecord OwnerRecord(SeenFile f, string gameId)
        {
            for (int i = 0; f != null && !string.IsNullOrEmpty(gameId) && i < f.Records.Count; i++)
            {
                if (SeenRules.IsOwnerKey(f.Records[i].Key, gameId))
                {
                    return f.Records[i];
                }
            }
            return null;
        }

        private static SeenRecord TycoonRecord(SeenFile f, int index)
        {
            for (int i = 0; f != null && i < f.Records.Count; i++)
            {
                if (SeenRules.IsTycoonKey(f.Records[i].Key, index))
                {
                    return f.Records[i];
                }
            }
            return null;
        }

        private static SeenSubject Subject(IList<SeenSubject> subjects, string key)
        {
            for (int i = 0; subjects != null && i < subjects.Count; i++)
            {
                if (subjects[i] != null && string.Equals(subjects[i].Key, key, StringComparison.Ordinal))
                {
                    return subjects[i];
                }
            }
            return null;
        }
    }
}
