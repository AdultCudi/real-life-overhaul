using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// RLO Texts (Scripts/Texts) with the features whose messages it words, one line each:
        ///
        ///   Flare      the posts that go up later (the city's, the replies, the answers, the rivals', the businesses'), the
        ///              friends' replies and the answers to the player's messages: asked through FlareBridges.TextAsk, shown
        ///              through FlareBridges.TextKept (Social/Flare/FlareDirector.Texts.cs)
        ///   the phone  friends, the partner, Mom, the in-laws, roommates, the boss and the lender: a text held back a moment
        ///              for its words (TextsPhone), sent with the written line when they are not there in time
        ///   calls      a friend's or the partner's answer to what the player types on a call (TextsCalls)
        ///   face to face  a friend's greeting at an outing, guessed when the player walks up and taken at the click
        ///   answers    with a v3 pack, the authors' answers under the player's typed replies and the answers to typed
        ///              messages and mentions (Social/Flare/FlareDirector.Answers.cs, FlareDirector.Texts.cs)
        ///   the paper  the City Ledger's economy briefs, asked when an edition is written (at dawn); the words are used only if
        ///              they come before any piece of that edition has been shown, so a page never changes in front of the player
        ///   kept texts let go once the game no longer has what they belong to (<see cref="StillThere"/>)
        ///
        /// COST: nothing per frame. A request is made when the feature makes its message, never on a timer of its own.
        /// </summary>
        public static class TextsLinks
        {
            public const double LedgerSeconds = 120.0;   // real seconds an edition's briefs may take: written at dawn, read later

            private static readonly HashSet<int> LedgerShown = new HashSet<int>();   // the editions (by day) a piece of which has been shown

            internal static void Connect()
            {
                FlareBridges.TextAsk = r => TextsDirector.Words.Ask(r, TextClock.Now);
                FlareBridges.TextKept = key => TextsDirector.Words.Store.Words(key);
                FlareBridges.TextWants = (use, shared) => TextsDirector.Words.Wants(use, shared);
                FlareBridges.TextWhen = TextsGame.When;
                FlareBridges.TextSeconds = TextsGame.SecondsFor;
                FlareBridges.TextData = () => TextsDirector.Words.Data;
                FlareBridges.TextFriend = TextsPhone.FlareFriend;
                FlareBridges.TextListed = Listed;
                LedgerBridges.TextsEdition = Edition;
                LedgerBridges.TextsBody = Body;
                TextsDirector.StillThere = StillThere;
                LedgerShown.Clear();
                TextsPhone.Connect();
                TextsCalls.Connect();
            }

            internal static void Disconnect()
            {
                FlareBridges.TextAsk = null;
                FlareBridges.TextKept = null;
                FlareBridges.TextWants = null;
                FlareBridges.TextWhen = null;
                FlareBridges.TextSeconds = null;
                FlareBridges.TextData = null;
                FlareBridges.TextFriend = null;
                FlareBridges.TextListed = null;
                LedgerBridges.TextsEdition = null;
                LedgerBridges.TextsBody = null;
                TextsDirector.StillThere = null;
                TextsPhone.Disconnect();
                TextsCalls.Disconnect();
            }

            // The player's listed company's name (a rival's stake in it or bid for it names it); null while there is none.
            private static string Listed()
            {
                Markets.Stock own = Markets.MarketDirector.Instance != null ? Markets.MarketDirector.Instance.MyCompany : null;
                return own != null && !string.IsNullOrEmpty(own.Name) ? own.Name : null;
            }

            // A piece's words as the page draws it; drawing marks its edition shown, after which no new words are taken for it.
            private static string Body(LedgerPiece p)
            {
                int day = LedgerDirector.Instance != null ? LedgerDirector.Instance.DayOf(p) : -1;
                if (p == null || day < 0)
                {
                    return null;
                }
                LedgerShown.Add(day);
                return TextsDirector.Words.Store.Words(TextsMapRules.LedgerKey(day, p.Key, p.Data));
            }

            // The City Ledger's briefs of an edition: the economy's (prices, the bank rate, a district's fortunes), asked as it is written.
            private static void Edition(LedgerEdition e)
            {
                try
                {
                    bool shared = GoingPublic.InSession;
                    if (e == null || !TextsDirector.Words.Wants(TextUse.Ledger, shared))
                    {
                        return;
                    }
                    int day = e.Day;
                    LedgerShown.Remove(day);   // (written again: a new edition of the day)
                    for (int i = 0; i < e.Pieces.Count; i++)
                    {
                        LedgerPiece p = e.Pieces[i];
                        string intent = p != null ? TextsMapRules.Ledger(p.Key) : null;
                        if (intent == null)
                        {
                            continue;
                        }
                        var r = new TextRequest
                        {
                            Use = TextUse.Ledger, Intent = intent, Key = TextsMapRules.LedgerKey(day, p.Key, p.Data), Speaker = "ledger", Priority = TextPriority.Background,
                            Seconds = LedgerSeconds, Keep = !shared, Shared = shared, Day = day, Done = words => { },
                            Wanted = () => !LedgerShown.Contains(day)   // (once a piece of the edition is shown, its words stay as shown)
                        };
                        r.Who.When = TextsGame.When();
                        string change = p.Value("change"), points = p.Value("points"), district = p.Value("district");
                        if (change != null)
                        {
                            r.Slots.Set("change", change);
                        }
                        if (points != null)
                        {
                            r.Slots.Set("points", points);
                        }
                        if (district != null)
                        {
                            r.Slots.Set("district", district);
                        }
                        TextsDirector.Words.Ask(r, TextClock.Now);
                    }
                }
                catch (Exception ex)
                {
                    Fault("RLO Texts, the City Ledger", ex);
                }
            }

            /// <summary>
            /// Whether the game still has what a kept text belongs to: a Flare post ("fp:id"), a friend's reply under one
            /// ("fr:post:person"), a comment under one ("fc:post:slot:who"), the answer under a reply of the player's ("fa:reply"), a message ("fd:with:at"), an edition of the paper ("lp:day:hash"). While Flare or the paper is
            /// not running its texts are kept (nothing is judged on a feature that is not there to ask).
            /// </summary>
            public static bool StillThere(string key)
            {
                try
                {
                    if (key == null || key.Length < 4)
                    {
                        return false;
                    }
                    string[] f = key.Split(':');
                    switch (f[0])
                    {
                        case "fp":
                        case "fr":
                        case "fc":
                        case "fa":
                            FlareFile file = FlareDirector.Instance?.File;
                            return file == null || (f.Length >= 2 && int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && file.Post(id) != null);
                        case "fd":
                            FlareFile dms = FlareDirector.Instance?.File;
                            if (dms == null)
                            {
                                return true;
                            }
                            int cut = key.LastIndexOf(':');
                            string with = key.Substring(3, Math.Max(0, cut - 3));
                            if (!long.TryParse(key.Substring(cut + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long at))
                            {
                                return false;
                            }
                            for (int i = 0; i < dms.Net.Dms.Count; i++)
                            {
                                if (dms.Net.Dms[i].At == at && dms.Net.Dms[i].With == with)
                                {
                                    return true;
                                }
                            }
                            return false;
                        case "lp":
                            LedgerDirector ledger = LedgerDirector.Instance;
                            return ledger == null || ledger.State == null
                                   || (f.Length >= 2 && int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day) && ledger.HasDay(day));
                        default:
                            return false;   // (nothing else is kept)
                    }
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, letting kept texts go", e);
                    return true;
                }
            }
        }
    }
}
