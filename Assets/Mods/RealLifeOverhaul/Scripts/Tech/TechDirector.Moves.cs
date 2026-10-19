using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Moves: a business the game's moving company moved to another building takes its Tech data along (MoveRules). Asked as
    /// each part starts, at the start of a day, on every read of the firms and on the hour a booked move is made; each ask is
    /// one pass over the buildings and the parts' files, and the game's messages are read only when data has lost its business
    /// and that place was not asked about already this session. No frame cost.
    /// </summary>
    public sealed partial class TechDirector
    {
        private readonly List<BusinessNow> _now = new List<BusinessNow>(32);
        private readonly List<string> _loose = new List<string>(4);
        private readonly List<MoveSeen> _movesSeen = new List<MoveSeen>(4);
        private readonly HashSet<string> _moveAsked = new HashSet<string>(StringComparer.Ordinal);   // places with no move found for them this session
        private int _movesBooked = -1;

        // The parts running now, as MoveRules marks them.
        private int MoveParts()
        {
            return MoveRules.PartPeople | (_hiringOn ? MoveRules.PartHiring : 0) | (_firmsOn ? MoveRules.PartFirms : 0) | (_ventureOn ? MoveRules.PartVenture : 0);
        }

        /// <summary>The parts follow any move of the player's businesses that left their data where no business stands. Returns the moves followed.</summary>
        private int FollowMoves()
        {
            if (!_peopleOn || _talent == null || Save == null)
            {
                return 0;
            }
            MovesGame.Businesses(_now);
            for (int i = 0; i < _now.Count; i++)
            {
                _moveAsked.Remove(_now[i].Place);   // a business stands there again: it may move away later
            }
            FirmsFile firms = _firmsOn ? _firmsFile : null;
            HiringFile hiring = _hiringOn ? _hiring : null;
            VentureFile venture = _ventureOn ? _venture : null;
            MoveRules.Loose(firms, _talent, hiring, venture, _now, Save.Day, _loose);
            for (int i = _loose.Count - 1; i >= 0; i--)
            {
                if (_moveAsked.Contains(_loose[i]))
                {
                    _loose.RemoveAt(i);
                }
            }
            if (_loose.Count == 0)
            {
                return 0;
            }
            MovesGame.Moves(_movesSeen, _loose);
            int followed = 0;
            for (int i = 0; i < _loose.Count; i++)
            {
                string from = _loose[i];
                MoveVerdict verdict = MoveRules.Judge(from, _movesSeen, firms, _talent, _now, out MoveSeen move, out string to);
                if (verdict == MoveVerdict.Later)
                {
                    continue;
                }
                if (verdict == MoveVerdict.No)
                {
                    _moveAsked.Add(from);
                    if (Verbose)
                    {
                        Logger?.Info("Tech check (moves, day " + Save.Day + "): data at " + Where(from) + " has no business there; " + _movesSeen.Count
                                     + " move(s) in the moving companies' messages, none that took its business elsewhere without doubt: left as it is: PASS");
                    }
                    continue;
                }
                MoveCount n = MoveRules.Follow(from, to, firms, _talent, hiring, venture, Save.Day);
                MoveRules.Record(_talent, from, to, move.Day, move.Hour, MoveParts(), n.Firm > 0, n.Closed > 0);
                followed++;
                Logger?.Info("Tech check (move, day " + Save.Day + "): " + move.Name + " was moved by the moving company from " + Where(from) + " to " + Where(to) + " on day " + move.Day
                             + "; followed by " + Counts(n) + " (wanted: everything at the old building at the new one, none left behind): "
                             + (Left(from) == 0 ? "PASS" : Left(from) + " record(s) still at the old building"));
            }
            return followed;   // the callers write the apps again (not from inside a read of the firms)
        }

        /// <summary>A part starting follows the moves made while it was not running.</summary>
        private void CatchUpMoves(int part)
        {
            if (_talent == null || Save == null)
            {
                return;
            }
            var n = new MoveCount();
            int followed = MoveRules.CatchUp(_talent, part, part == MoveRules.PartFirms ? _firmsFile : null, part == MoveRules.PartHiring ? _hiring : null,
                part == MoveRules.PartVenture ? _venture : null, Save.Day, ref n);
            if (n.Total > 0)
            {
                Logger?.Info("Tech check (moves on start, day " + Save.Day + "): " + followed + " earlier move(s) followed by " + Counts(n) + ": PASS");
            }
        }

        // On the hour: a booked move made since the last hour is followed now, not the next morning.
        private void MovesHour()
        {
            int booked = MovesGame.Booked();
            if (_movesBooked >= 0 && booked < _movesBooked && FollowMoves() > 0)
            {
                Changed();
            }
            _movesBooked = booked;
        }

        // Records of the parts' data still at a place (after following a move from it: wanted none).
        private int Left(string place)
        {
            int n = 0;
            FirmsFile f = _firmsOn ? _firmsFile : null;
            for (int i = 0; f != null && i < f.Firms.Count; i++)
            {
                n += f.Firms[i].Place == place ? 1 : 0;
            }
            for (int i = 0; f != null && i < f.Products.Count; i++)
            {
                n += f.Products[i].Place == place ? 1 : 0;
            }
            for (int i = 0; _talent != null && i < _talent.People.Count; i++)
            {
                n += _talent.People[i].Place == place ? 1 : 0;
            }
            for (int i = 0; _hiringOn && _hiring != null && i < _hiring.Ads.Count; i++)
            {
                n += _hiring.Ads[i].Place == place ? 1 : 0;
            }
            return n;
        }

        private static string Counts(MoveCount n)
        {
            return n.Firm + " firm, " + n.Products + " product(s), " + n.Contracts + " contract(s), " + n.Clients + " contract(s) for it as a client, " + n.People + " professional(s), "
                   + n.Ads + " ad(s), " + n.Caps + " cap table(s)" + (n.Closed > 0 ? "; " + n.Closed + " firm at the new building closed, its business replaced" : string.Empty)
                   + (n.CapsDropped > 0 ? ", " + n.CapsDropped + " cap table(s) of a replaced firm dropped" : string.Empty);
        }

        private static string Where(string place)
        {
            return PlaceRules.Street(place, out string street) && PlaceRules.Number(place, out int number) ? street + " " + number : place;
        }

        /// <summary>The options page's test: the first move booked with a moving company is made now, and the Tech parts follow it at once.</summary>
        public void TestMove()
        {
            if (!_peopleOn || Save == null)
            {
                Notifications.ShowError("reallife_tech_test_off");
                return;
            }
            try
            {
                if (!MovesGame.MoveNow())
                {
                    Notifications.ShowError("reallife_tech_test_no_move");
                    return;
                }
                _movesBooked = MovesGame.Booked();
                FollowMoves();
                if (_firmsOn)
                {
                    ReadFirms(Save.Day);
                }
                Changed();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
