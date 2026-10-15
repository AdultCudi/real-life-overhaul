using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The City Ledger in a Going Public session (Core/SharedCity, part L; the lines are LedgerShare's): one paper for the
    /// city. The host prints the morning's edition as alone and it goes to every guest whole when it is printed (and to a
    /// guest that says hello); a guest that follows prints none of its own and reads the host's. What would be news on a
    /// guest's street (a crash, an arrest, a crime) is reported to the host's paper as it happens, and a guest's own pieces
    /// of the morning (their day in court, a graduation, their business, Flare) go to the host for that day's edition,
    /// which the host prints again with them (once in twenty seconds at most). So the paper has every player's stories.
    ///
    /// COST: nothing per frame. The host: the edition's text when it is printed (some ten kilobytes, deflated by the shared
    /// city). A guest: a line for each report as it happens; its pieces once a morning.
    /// </summary>
    public sealed partial class LedgerDirector
    {
        private const float ReprintWait = 20f;   // real seconds after a guest's piece came before today's edition is printed again with it

        private readonly List<KeyValuePair<string, LedgerPiece>> _guestPieces = new List<KeyValuePair<string, LedgerPiece>>(8);
        private readonly List<LedgerPiece> _taken = new List<LedgerPiece>(8);
        private int _guestPiecesDay = -1, _sentPiecesDay = -1;
        private float _reprintAt = -1f;

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

        // A guest: the host's edition is this player's paper; the one it replaces is kept to be read again.
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
                return true;   // an older one than this player has: nothing to do
            }
            KeepEarlier(_state.Edition, e.Day);
            _state.Edition = e;
            _page?.Changed();
            _stand?.Changed();
            Logger?.Info("City Ledger: the host's edition of day " + e.Day + " (" + e.Pieces.Count + " pieces) is this player's paper.");
            return true;
        }

        // The host: a guest's report for the city's paper, and a guest's own pieces for today's edition.
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
                    if (piece != null && _guestPieces.Count < 32)
                    {
                        _guestPieces.Add(new KeyValuePair<string, LedgerPiece>(who, piece));
                        _reprintAt = Time.unscaledTime + ReprintWait;
                    }
                }
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

        // The guests' pieces of the day in the edition being put together.
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

        // A guest, once a morning: its own pieces about its player (from its own paper and the features' extras) to the host.
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

        /// <summary>
        /// Something the paper would print happened here (Social/News, through the wiring): on a guest that follows the host's
        /// paper it goes to the host's, so that the city's one paper has it. Nothing otherwise. A line, as it happens.
        /// </summary>
        public void GuestReport(int kind, float x, float z, string detail, int amount)
        {
            if (Running && SharedCity.Follows(LedgerShare.Part))
            {
                SharedCity.Ask(LedgerShare.Part, LedgerShare.AskReport(kind, x, z, detail, amount));
            }
        }
    }
}
