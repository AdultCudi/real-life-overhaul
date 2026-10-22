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
        /// RLO Texts with the features whose messages it words: Flare, the phone's texts (TextsPhone), calls (TextsCalls),
        /// the city's talk (TextsCity) and the City Ledger's briefs, whose words are used only if they come before a piece
        /// of that edition is shown, so a page never changes in front of the player. Nothing per frame: a request is made
        /// when a feature makes its message.
        /// </summary>
        public static class TextsLinks
        {
            public const double LedgerSeconds = 120.0;   // real seconds an edition's briefs may take: written at dawn, read later

            private static readonly HashSet<int> LedgerShown = new HashSet<int>();   // the editions (by day) a piece of which has been shown
            private static Action<NewsKind, UnityEngine.Vector3> _onNews;

            internal static void Connect()
            {
                FlareBridges.TextAsk = AskFlare;
                FlareBridges.TextKept = key => TextsDirector.Words.Store.Words(key, TextsDirector.Words.Language);   // data v6's words only in their own language
                FlareBridges.TextWants = (use, shared) => TextsDirector.Words.Wants(use, shared);
                FlareBridges.TextWhen = TextsGame.When;
                FlareBridges.TextSeconds = TextsGame.SecondsFor;
                FlareBridges.TextData = () => TextsDirector.Words.Data;
                FlareBridges.TextFriend = TextsPhone.FlareFriend;
                FlareBridges.TextListed = Listed;
                FlareBridges.TextReview = Review;
                LedgerBridges.TextsEdition = Edition;
                LedgerBridges.TextsBody = Body;
                TextsDirector.StillThere = StillThere;
                LedgerShown.Clear();
                News.ReportedAt -= _onNews ?? (_onNews = Heard);
                News.ReportedAt += _onNews;
                TextsPhone.Connect();
                TextsCalls.Connect();
                TextsCity.Connect();
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
                FlareBridges.TextReview = null;
                LedgerBridges.TextsEdition = null;
                LedgerBridges.TextsBody = null;
                TextsDirector.StillThere = null;
                if (_onNews != null)
                {
                    News.ReportedAt -= _onNews;
                }
                TextsPhone.Disconnect();
                TextsCalls.Disconnect();
                TextsCity.Disconnect();
            }

            // A Flare request: its person's memory on the card (a message's, never a post's), and what it was about remembered.
            private static bool AskFlare(TextRequest r)
            {
                SocialPerson friend = null;
                if (r != null && r.Speaker != null && r.Speaker.Length > 1 && (r.Speaker[0] == 'f' || r.Speaker[0] == 'p')
                    && int.TryParse(r.Speaker.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && SocialState.Loaded)
                {
                    friend = SocialState.Get(index);
                }
                TextsMemory.Fill(r, friend);
                bool asked = TextsDirector.Words.Ask(r, TextClock.Now);
                TextsMemory.Spoke(r, "dm");
                return asked;
            }

            // The player's own crash, arrest or knock-down, for the people of their life to hear of.
            private static void Heard(NewsKind kind, UnityEngine.Vector3 where)
            {
                try
                {
                    switch (kind)
                    {
                        case NewsKind.PlayerCrash: TextsDirector.Heard("crash", SocialPlaces.StreetAt(where)); break;
                        case NewsKind.PlayerArrested: TextsDirector.Heard("arrest", null); break;
                        case NewsKind.PlayerKnocked: TextsDirector.Heard("knocked", SocialPlaces.StreetAt(where)); break;
                    }
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, the player's events", e);
                }
            }

            // A review of the player's own business: the game's figures of it on the card; false for another's (the plain card).
            private static bool Review(string place, int stars, TextSlots slots)
            {
                try
                {
                    BuildingRegistration reg = FlareGame.Business(place);
                    if (reg == null || !TextsGame.Figures(reg, out int service, out int pricing, out int cleanliness, out int interior, out int crowd))
                    {
                        return false;
                    }
                    slots.Set("service", TextWords.Rating(service)).Set("prices", TextWords.Rating(pricing)).Set("cleanliness", TextWords.Rating(cleanliness))
                        .Set("interior", TextWords.Rating(interior)).Set("about", TextsCityRules.ReviewAbout(stars, service, pricing, cleanliness, interior));
                    if (crowd != 0)
                    {
                        slots.Set("crowd", crowd > 0 ? "busy" : "quiet");
                    }
                    return true;
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, a review's figures", e);
                    return false;
                }
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
                return TextsDirector.Words.Store.Words(TextsMapRules.LedgerKey(day, p.Key, p.Data), TextsDirector.Words.Language);
            }

            // The City Ledger's economy briefs of an edition, asked as it is written.
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
                    LedgerShown.Remove(day);   // written again: a new edition of the day
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
                            Wanted = () => !LedgerShown.Contains(day)   // once a piece of the edition is shown, its words stay as shown
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
            /// Whether the game still has what a kept text belongs to: a Flare post ("fp:id"), a reply ("fr:post:person"), a
            /// comment ("fc:post:slot:who"), an answer ("fa:reply"), a message ("fd:with:at") or an edition ("lp:day:hash").
            /// While Flare or the paper is not running, its texts are kept.
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
                            return false;   // nothing else is kept
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
