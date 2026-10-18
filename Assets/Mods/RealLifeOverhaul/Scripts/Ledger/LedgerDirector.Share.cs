using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The director's Going Public part (Core/SharedCity, part L): one paper for the city, printed by the host; a guest
    /// that follows sends its news and pieces to the host. The cost: nothing per frame; about 10 KB each print.
    /// </summary>
    public sealed partial class LedgerDirector
    {
        private const float ReprintWait = 20f;   // real seconds after a guest's piece came before today's edition is printed again with it

        private readonly List<KeyValuePair<string, LedgerPiece>> _guestPieces = new List<KeyValuePair<string, LedgerPiece>>(8);
        private readonly List<LedgerPiece> _taken = new List<LedgerPiece>(8);
        private int _guestPiecesDay = -1, _sentPiecesDay = -1;
        private float _reprintAt = -1f;
        private bool _hostEdition;                 // a guest: the edition shown is the host's
        private LedgerEdition _ownToday;           // and its own of that day, back if it stops following

        private void ShareRegister()
        {
            SharedCity.Register(new SharedCityPart
            {
                Id = LedgerShare.Part, Name = "City Ledger",
                Ready = () => Running && Save != null,
                Write = () => Running ? LedgerShare.Write(_state.Edition) : null,
                Apply = GuestApply,
                Request = HostRequest
            });
        }

        // A guest: the host's edition becomes this player's paper; the one it replaces is kept to be read again.
        private bool GuestApply(string text, bool whole)
        {
            if (!Running)
            {
                return false;
            }
            LedgerEdition e = LedgerShare.Read(text);
            if (e == null)
            {
                return false;
            }
            if (_state.Edition != null && e.Day < _state.Edition.Day)
            {
                return true;   // older than the one this player has
            }
            if (_state.Edition != null && _state.Edition.Day == e.Day && !_hostEdition)
            {
                _ownToday = _state.Edition;   // back if it stops following today
            }
            _hostEdition = true;
            KeepEarlier(_state.Edition, e.Day);
            _state.Edition = e;
            _page?.Changed();
            _stand?.Changed();
            Logger?.Info("City Ledger: the host's edition of day " + e.Day + " (" + e.Pieces.Count + " pieces) is this player's paper.");
            return true;
        }

        // The host: a guest's report for the city's paper, or a guest's own piece for today's edition.
        private void HostRequest(string text, string who)
        {
            if (!Running || Save == null || string.IsNullOrEmpty(text))
            {
                return;
            }
            string[] lines = text.Split('\n');
            int day = Save.Day;
            if (_guestPiecesDay != day)
            {
                _guestPiecesDay = day;
                _guestPieces.Clear();
            }
            for (int i = 0; i < lines.Length && i < LedgerShare.AsksMost; i++)
            {
                string[] f = lines[i].TrimEnd('\r').Split('|');
                if (f.Length == 0)
                {
                    continue;
                }
                if (f[0] == "N")
                {
                    int kind, amount;
                    float x, z;
                    string detail;
                    if (LedgerShare.ReadReport(f, (int)NewsKind.Story + 1, out kind, out x, out z, out detail, out amount))
                    {
                        LedgerBridges.Report?.Invoke(kind, x, z, detail, amount);
                    }
                }
                else if (f[0] == "P")
                {
                    LedgerPiece piece = LedgerShare.ReadPiece(f);
                    if (LedgerShare.Admit(_guestPieces, who, piece, LedgerBridges.Filter != null ? text2 => LedgerBridges.Filter(text2, who) : (Func<string, string>)null))
                    {
                        _reprintAt = LedgerShare.ReprintAt(_reprintAt, Time.unscaledTime, ReprintWait);
                    }
                }
            }
        }

        // No longer following: this player's own edition of today comes back.
        private void OwnAgain(int day)
        {
            if (!_hostEdition)
            {
                return;
            }
            _hostEdition = false;
            LedgerEdition own = _ownToday;
            _ownToday = null;
            if (own != null && own.Day == day && _state.Edition != null && _state.Edition.Day == day)
            {
                _state.Edition = own;
                _page?.Changed();
                _stand?.Changed();
                Logger?.Info("City Ledger: no longer the host's paper; this player's own edition of day " + day + " is back.");
            }
        }

        // The host: today's edition printed again with the guests' pieces, a little after they came.
        private void Reprint(int day, int hour)
        {
            if (_reprintAt < 0f || Time.unscaledTime < _reprintAt || _state.Edition == null || _state.Edition.Day != day)
            {
                return;
            }
            _reprintAt = -1f;
            Print(day, hour, true);
        }

        private void GuestPiecesInto(LedgerEdition e, int day)
        {
            if (_guestPiecesDay != day || _guestPieces.Count == 0)
            {
                return;
            }
            _taken.Clear();
            LedgerShare.Take(_guestPieces, _taken);
            e.Pieces.AddRange(_taken);
        }

        // A guest, once a morning: its own pieces about its player go to the host.
        private void GuestPieces(int day, int hour)
        {
            if (_sentPiecesDay == day || hour < LedgerCatalogue.DeliveryHour)
            {
                return;
            }
            int paperDay = LedgerBridges.PaperDay != null ? LedgerBridges.PaperDay() : 0;
            if (paperDay != day && hour < LedgerCatalogue.DeliveryHour + 1)
            {
                return;
            }
            _sentPiecesDay = day;
            _scratch.Clear();
            if (paperDay == day)
            {
                LedgerBridges.Paper?.Invoke(_scratch);
            }
            LedgerBridges.Extra?.Invoke(day, _scratch);
            int sent = 0;
            for (int i = 0; i < _scratch.Count && sent < LedgerShare.PiecesPerGuestDay; i++)
            {
                string line = LedgerShare.AskPiece(_scratch[i]);
                if (line != null)
                {
                    SharedCity.Ask(LedgerShare.Part, line);
                    sent++;
                }
            }
            if (sent > 0)
            {
                Logger?.Info("City Ledger: " + sent + " piece(s) about this player go to the host's paper of day " + day + ".");
            }
        }

        /// <summary>News that happened here: on a guest following the host's paper it goes to the host, as it happens.</summary>
        public void GuestReport(int kind, float x, float z, string detail, int amount)
        {
            string line = Running && SharedCity.Follows(LedgerShare.Part) ? LedgerShare.AskReport(kind, x, z, detail, amount) : null;
            if (line != null)
            {
                SharedCity.Ask(LedgerShare.Part, line);   // the city's kinds only: the player's own go as pieces
            }
        }
    }
}
