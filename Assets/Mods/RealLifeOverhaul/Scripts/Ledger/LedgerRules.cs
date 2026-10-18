using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The City Ledger's rules: a piece's kind and words, the subscription's round, the copy, and which pieces go where
    /// on the page. Game-free: the checks run it as it ships.
    /// </summary>
    public static class LedgerRules
    {
        /// <summary>The kind a key is, from the catalogue's table; a key it does not know is a piece for the city's pages.</summary>
        public static LedgerCatalogue.Kind KindOf(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                LedgerCatalogue.Kind[] kinds = LedgerCatalogue.Kinds;
                for (int i = 0; i < kinds.Length; i++)
                {
                    if (key.StartsWith(kinds[i].Prefix, StringComparison.Ordinal))
                    {
                        return kinds[i];
                    }
                }
            }
            return new LedgerCatalogue.Kind(key ?? string.Empty, LedgerSection.City, LedgerCatalogue.UnknownWeight, null);
        }

        /// <summary>A piece for a key and its names, in its section with its weight and headline; null for one the page leaves out.</summary>
        public static LedgerPiece Piece(string key, string[] data, int extraWeight = 0)
        {
            LedgerCatalogue.Kind kind = KindOf(key);
            if (kind.Weight < 0)
            {
                return null;
            }
            return new LedgerPiece(kind.Section, kind.Weight + extraWeight, key, kind.Head, data);
        }

        /// <summary>
        /// Divides a piece's words as the paper writes them: a kicker in capitals ending with a colon ("COURT:"), a
        /// headline opening with a word in capitals up to the first full stop ("SERIOUS CRASH ON Elm Street."), then
        /// the story; a piece without them is a brief. The headline comes back as written.
        /// </summary>
        public static void Split(string text, out string kicker, out string headline, out string body)
        {
            kicker = null;
            headline = null;
            body = (text ?? string.Empty).Trim();
            if (body.Length == 0)
            {
                return;
            }
            int colon = body.IndexOf(':');
            if (colon > 0 && colon < 34 && (colon + 1 == body.Length || body[colon + 1] == ' ') && CapitalWords(body, 0, colon) && Words(body, 0, colon) <= 4)
            {
                kicker = body.Substring(0, colon).Trim();
                body = body.Substring(colon + 1).Trim();
            }
            if (body.Length == 0 || !CapitalStart(body))
            {
                return;
            }
            int stop = Stop(body);
            if (stop <= 0 || stop >= body.Length - 1 || stop > 120)
            {
                return;   // a sentence of capitals with nothing after it is a story, not a headline
            }
            headline = body.Substring(0, stop).Trim();
            body = body.Substring(stop + 1).Trim();
        }

        // The first full stop that ends a sentence: followed by a space (not "09.30", not "3.5%").
        private static int Stop(string text)
        {
            for (int i = 0; i < text.Length - 1; i++)
            {
                if (text[i] == '.' && text[i + 1] == ' ')
                {
                    return i;
                }
            }
            return -1;
        }

        // True if the text opens in capitals: its first word all capitals, and the second as well when the first is a
        // single letter ("A QUIET DAY."), so that a sentence that merely begins with "A" or "I" is not taken for one.
        private static bool CapitalStart(string text)
        {
            int letters = 0;
            int words = 0;
            bool inWord = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ' || c == ':' || c == '.' || c == ',')
                {
                    if (inWord)
                    {
                        inWord = false;
                        words++;
                        if (letters >= 2 || words >= 2)
                        {
                            break;
                        }
                    }
                    if (c != ' ')
                    {
                        break;
                    }
                    continue;
                }
                inWord = true;
                if (char.IsLetter(c))
                {
                    if (!char.IsUpper(c))
                    {
                        return false;
                    }
                    letters++;
                }
            }
            return letters >= 2;
        }

        private static bool CapitalWords(string text, int from, int to)
        {
            int letters = 0;
            for (int i = from; i < to; i++)
            {
                char c = text[i];
                if (char.IsLetter(c))
                {
                    if (!char.IsUpper(c))
                    {
                        return false;
                    }
                    letters++;
                }
            }
            return letters >= 2;
        }

        private static int Words(string text, int from, int to)
        {
            int words = 0;
            bool inWord = false;
            for (int i = from; i < to; i++)
            {
                bool letter = text[i] != ' ';
                if (letter && !inWord)
                {
                    words++;
                }
                inWord = letter;
            }
            return words;
        }

        /// <summary>The first morning a subscription taken out now is delivered: today if the round has not been yet, else tomorrow.</summary>
        public static int FirstDelivery(LedgerState s, int day, int hour)
        {
            return hour < LedgerCatalogue.DeliveryHour && s.LastMorning < day ? day : day + 1;
        }

        public static bool CanSubscribe(LedgerState s)
        {
            return !s.Subscribed || s.Cancelled;
        }

        /// <summary>Takes a subscription out, or takes a cancelled one up again free (payNow false); false if one already runs.</summary>
        public static bool Subscribe(LedgerState s, out bool payNow)
        {
            payNow = false;
            if (s.Subscribed && s.Cancelled)
            {
                s.Cancelled = false;
                return true;
            }
            if (s.Subscribed)
            {
                return false;
            }
            payNow = true;
            return true;
        }

        /// <summary>After the first week has been paid for: the subscription runs.</summary>
        public static void Start(LedgerState s, int day, int hour)
        {
            int first = FirstDelivery(s, day, hour);
            s.Subscribed = true;
            s.Cancelled = false;
            s.Since = day;
            s.PaidUntil = first + LedgerCatalogue.WeekDays - 1;
            s.Weeks++;
        }

        /// <summary>Cancels the subscription: the paper still comes until the last day paid for. False if there is none to cancel.</summary>
        public static bool Cancel(LedgerState s)
        {
            if (!s.Subscribed || s.Cancelled)
            {
                return false;
            }
            s.Cancelled = true;
            return true;
        }

        /// <summary>
        /// The morning's round, once a day when the paper is out. Mornings it did not come are never charged: the days
        /// paid for move on by as many of them as were paid for (none past the week's end), as Newsstand's do. When they
        /// run out, a cancelled one ends and any other pays a week or lapses.
        /// </summary>
        public static LedgerMorning Morning(LedgerState s, int day, Func<bool> payWeek)
        {
            if (day <= s.LastMorning)
            {
                return LedgerMorning.None;
            }
            int missedPaid = s.Subscribed && s.LastMorning > 0 ? Math.Min(day - 1, s.PaidUntil) - s.LastMorning : 0;
            if (missedPaid > 0)
            {
                s.PaidUntil += missedPaid;
            }
            s.LastMorning = day;
            if (!s.Subscribed)
            {
                return LedgerMorning.None;
            }
            if (day <= s.PaidUntil)
            {
                return LedgerMorning.Kept;
            }
            if (s.Cancelled)
            {
                s.Subscribed = false;
                s.Cancelled = false;
                return LedgerMorning.Ended;
            }
            if (payWeek == null || !payWeek())
            {
                s.Subscribed = false;
                return LedgerMorning.Lapsed;
            }
            s.PaidUntil = day + LedgerCatalogue.WeekDays - 1;
            s.Weeks++;
            return LedgerMorning.Billed;
        }

        /// <summary>The next morning a week is paid for, while the subscription runs and is not cancelled; 0 otherwise.</summary>
        public static int NextPayment(LedgerState s)
        {
            return s.Subscribed && !s.Cancelled ? s.PaidUntil + 1 : 0;
        }

        public static void Deliver(LedgerState s, int day)
        {
            if (s.CopyDay != day)
            {
                s.CopyDay = day;
                s.Bought = false;
            }
            s.Delivered = true;
        }

        public static void Buy(LedgerState s, int day)
        {
            if (s.CopyDay != day)
            {
                s.CopyDay = day;
                s.Delivered = false;
            }
            s.Bought = true;
            s.Copies++;
        }

        /// <summary>True if the player has a copy of the latest edition, delivered or bought: it can be read at home.</summary>
        public static bool HasCopy(LedgerState s)
        {
            return s.Edition != null && s.CopyDay == s.Edition.Day && (s.Delivered || s.Bought);
        }

        /// <summary>True if the copy of the latest edition was bought: it can be read at the kiosk as well.</summary>
        public static bool BoughtCopy(LedgerState s)
        {
            return s.Edition != null && s.CopyDay == s.Edition.Day && s.Bought;
        }

        /// <summary>True if today's paper is out and on sale: the edition is today's and the kiosks are open.</summary>
        public static bool OnSale(LedgerState s, int day, int hour)
        {
            return s.Edition != null && s.Edition.Day == day && KioskOpen(hour);
        }

        public static bool KioskOpen(int hour)
        {
            return hour >= LedgerCatalogue.KioskOpenHour && hour < LedgerCatalogue.KioskCloseHour;
        }

        /// <summary>A section's pieces, the heaviest first (stable), but one: the lead, printed on its own.</summary>
        public static void Of(LedgerEdition e, LedgerSection section, LedgerPiece except, List<LedgerPiece> into)
        {
            into.Clear();
            for (int i = 0; i < e.Pieces.Count; i++)
            {
                LedgerPiece p = e.Pieces[i];
                if (p != null && p.Section == section && !ReferenceEquals(p, except))
                {
                    into.Add(p);
                }
            }
            Heaviest(into);
        }

        /// <summary>The front page's lead: the heaviest piece of the city, the economy, business or the markets; null for none.</summary>
        public static LedgerPiece Lead(LedgerEdition e)
        {
            LedgerPiece best = null;
            for (int i = 0; i < e.Pieces.Count; i++)
            {
                LedgerPiece p = e.Pieces[i];
                if (p == null || p.Section > LedgerSection.Markets)
                {
                    continue;
                }
                if (best == null || p.Weight > best.Weight)
                {
                    best = p;
                }
            }
            return best;
        }

        // A stable sort by weight, heaviest first (insertion: a section has a dozen pieces at most).
        private static void Heaviest(List<LedgerPiece> list)
        {
            for (int i = 1; i < list.Count; i++)
            {
                LedgerPiece p = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].Weight < p.Weight)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = p;
            }
        }

        /// <summary>The companies that rose (up) or fell most on the session, at most so many, the largest move first, ties by name.</summary>
        public static void Movers(LedgerEdition e, bool up, int most, List<LedgerQuote> into)
        {
            into.Clear();
            for (int i = 0; i < e.Quotes.Count; i++)
            {
                LedgerQuote q = e.Quotes[i];
                if (q != null && (up ? q.ChangeBp > 0 : q.ChangeBp < 0))
                {
                    into.Add(q);
                }
            }
            into.Sort((a, b) =>
            {
                int byMove = up ? b.ChangeBp.CompareTo(a.ChangeBp) : a.ChangeBp.CompareTo(b.ChangeBp);
                return byMove != 0 ? byMove : string.CompareOrdinal(a.Name, b.Name);
            });
            if (into.Count > most)
            {
                into.RemoveRange(most, into.Count - most);
            }
        }

        /// <summary>The companies most traded on the session by the money that changed hands, at most so many.</summary>
        public static void MostActive(LedgerEdition e, int most, List<LedgerQuote> into)
        {
            into.Clear();
            for (int i = 0; i < e.Quotes.Count; i++)
            {
                LedgerQuote q = e.Quotes[i];
                if (q != null && q.Volume > 0)
                {
                    into.Add(q);
                }
            }
            into.Sort((a, b) =>
            {
                int byMoney = ((double)b.Volume * b.Close).CompareTo((double)a.Volume * a.Close);
                return byMoney != 0 ? byMoney : string.CompareOrdinal(a.Name, b.Name);
            });
            if (into.Count > most)
            {
                into.RemoveRange(most, into.Count - most);
            }
        }

        /// <summary>The share prices as the paper prints them: by sector in the edition's order, each sector's companies by name.</summary>
        public static void Listing(LedgerEdition e, List<LedgerQuote> into)
        {
            into.Clear();
            into.AddRange(e.Quotes);
            into.RemoveAll(q => q == null);
            into.Sort((a, b) =>
            {
                int bySector = a.Sector.CompareTo(b.Sector);
                return bySector != 0 ? bySector : string.CompareOrdinal(a.Name, b.Name);
            });
        }
    }
}
