using System;
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
        ///   face to face  a friend's greeting at an outing, guessed when the player walks up and taken at the click
        ///   the paper  the City Ledger's briefs, written ahead of the morning's edition
        ///
        /// COST: nothing per frame. A request is made when the feature makes its message, never on a timer of its own.
        /// </summary>
        public static class TextsLinks
        {
            internal static void Connect()
            {
                FlareBridges.TextAsk = r => TextsDirector.Words.Ask(r, TextClock.Now);
                FlareBridges.TextKept = key => TextsDirector.Words.Store.Words(key);
                FlareBridges.TextWants = (use, shared) => TextsDirector.Words.Wants(use, shared);
                FlareBridges.TextWhen = TextsGame.When;
                FlareBridges.TextSeconds = TextsGame.SecondsFor;
                LedgerBridges.TextsEdition = Edition;
                LedgerBridges.TextsBody = p => p != null ? TextsDirector.Words.Store.Words(TextsMapRules.LedgerKey(p.Key, p.Data)) : null;
                TextsPhone.Connect();
            }

            internal static void Disconnect()
            {
                FlareBridges.TextAsk = null;
                FlareBridges.TextKept = null;
                FlareBridges.TextWants = null;
                FlareBridges.TextWhen = null;
                FlareBridges.TextSeconds = null;
                LedgerBridges.TextsEdition = null;
                LedgerBridges.TextsBody = null;
                TextsPhone.Disconnect();
            }

            public const double LedgerSeconds = 120.0;   // real seconds an edition's briefs may take: it is written at dawn and read later

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
                            Use = TextUse.Ledger, Intent = intent, Key = TextsMapRules.LedgerKey(p.Key, p.Data), Speaker = "ledger", Priority = TextPriority.Background,
                            Seconds = LedgerSeconds, Keep = !shared, Shared = shared, Day = e.Day, Done = words => { }
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
        }
    }
}
