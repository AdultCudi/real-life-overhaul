using System;
using System.Collections.Generic;
using Dialogs;
using Entities;
using Localizor;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Networking;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.Schemes;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The schemes with the features they sit in and the ones that catch them:
        ///
        ///   the company     the player's businesses, an invented claim by the insurer's own rules, the game's tax rate, a
        ///                   visit as business pressure's own audit, washed cash booked as a till's takings, and the quiet
        ///                   button on its pages (CompanyBridges.Quiet*)
        ///   the people      a club's stakes out of a district's households' savings and back into them (Population/PurseRules),
        ///                   only where the city's people are this machine's own
        ///   the courts      a file as a case on a summons, how it ended, the case that comes up next, a judge's weighing
        ///                   (Justice/SchemeCases); the Legal file's lines and what can be done (Justice.Bridges.File*)
        ///   Standing        exposure and a scandal, the push before the council and a councillor who carries it, a place
        ///                   on the list of a reception
        ///   the social life the quiet word on a call to somebody with a position, a favour bought (Networking), the
        ///                   friendship cooled by a refusal
        ///   the rivals      who is active and what they are worth, what a scheme, its findings and a conviction do to them
        ///   selling         what dressed books show a buyer, a buyer who sees through them, the sale that goes on the trail
        ///   the paper       the rivals' pieces, after the others'; the till's sales among a business's takings
        ///
        /// Each is called on a press or once a game day; a fault is said once (<see cref="Fault"/>) and the default answer is used.
        /// </summary>
        public static class SchemeLinks
        {
            private static readonly List<Favour> Asked = new List<Favour>(6);
            private static Action<int, List<NewsItem>> _paperBefore;
            private static Func<string, bool> _takingsBefore;
            private static Action<string, int> _soldBefore;

            internal static void Connect()
            {
                SchemeBridges.Shops = Shops;
                SchemeBridges.Claim = Claim;
                SchemeBridges.CoverMost = kind => CompanyCatalogue.CoverMost[CompanyCatalogue.Clamp(kind, 0, CompanyCatalogue.CoverMost.Length - 1)];
                SchemeBridges.ClaimUnpaid = id => CompanyDirector.Instance != null && CompanyDirector.Instance.ClaimUnpaid(id);
                SchemeBridges.ClaimRefuse = id => CompanyDirector.Instance != null && CompanyDirector.Instance.ClaimFoundOut(id);
                SchemeBridges.TaxPercent = () => CompanyDirector.Instance != null && CompanyDirector.Instance.On(CompanyPart.Tax) ? CompanyGame.TaxPercent : 0;
                SchemeBridges.Taxed = () => CompanyDirector.Instance != null && CompanyGame.Taxed(CompanyDirector.Instance.Today - 1);
                SchemeBridges.Visit = Visit;
                SchemeBridges.People = () => CityPopulation.Running && !CityPopulation.Following && !GoingPublic.InSession;
                SchemeBridges.Club = Club;
                SchemeBridges.Restore = Restore;
                SchemeBridges.Court = () => Justice.Active;
                SchemeBridges.Charge = (charge, evidence, amount, seed) => SchemeCases.File(charge, evidence / 1000f, amount, seed, evidence >= SchemeCatalogue.CoopEvidence);
                SchemeBridges.CaseEnd = CaseEnd;
                SchemeBridges.NextCase = SchemeCases.Next;
                SchemeBridges.Lean = (number, less) => SchemeCases.Lean(number, less / 1000f);
                SchemeBridges.Exposed = scandal => FameDirector.Instance?.Exposed(scandal);
                SchemeBridges.Integrity = delta => FameDirector.Instance?.Moved(FamePart.Integrity, FameCause.Scandal, delta);
                SchemeBridges.PushPending = () => FameDirector.Instance != null ? FameDirector.Instance.PushPending() : 0;
                SchemeBridges.PushFee = () => FameDirector.Instance != null ? FameDirector.Instance.PushFee() : 0L;
                SchemeBridges.PushCarry = () => FameDirector.Instance != null && FameDirector.Instance.PushCarry();
                SchemeBridges.Invite = () => FameDirector.Instance != null && FameDirector.Instance.BoughtInvite();
                SchemeBridges.Council = Council;
                SchemeBridges.RoleOf = RoleOf;
                SchemeBridges.Cooled = Cooled;
                SchemeBridges.Rivals = Rivals;
                SchemeBridges.RivalName = tycoon => RivalCatalogue.Tycoons[RivalCatalogue.Clamp(tycoon, 0, RivalCatalogue.TycoonCount - 1)].Name;
                SchemeBridges.RivalHolding = tycoon => RivalCatalogue.Tycoons[RivalCatalogue.Clamp(tycoon, 0, RivalCatalogue.TycoonCount - 1)].Holding;
                SchemeBridges.RivalMoney = RivalMoney;
                SchemeBridges.RivalBarred = RivalBarred;
                SchemeBridges.Worth = Books.NetWorth;
                SchemeBridges.Floor = () => Tech.TechDirector.Instance != null ? Tech.TechDirector.Instance.FloorNow : 0L;
                SchemeBridges.Trended = () => FlareDirector.Instance?.Happened(FlareSource.Arrested, -1, null, null, 0);
                SchemeBridges.Story = Paper.Print;
                SchemeBridges.Till = Till;
                SchemeBridges.Letter = (key, data) => LegalPhone.Send(key, data);
                CompanyBridges.QuietLabel = SchemeDirector.QuietLabel;
                CompanyBridges.Quiet = SchemeDirector.QuietOpen;
                ServicesBridges.BooksLift = reg => reg != null ? SchemeDirector.BooksLift(CompanyGame.Place(reg)) : 0;
                ServicesBridges.SeesThrough = SchemeDirector.SaleSeen;
                _soldBefore = ServicesBridges.Sold;
                ServicesBridges.Sold = Sold;
                SocialPhone.QuietLabel = PersonLabel;
                SocialPhone.QuietAsk = PersonAsk;
                NetworkBridges.Bought = SchemeDirector.Bought;
                NetworkBridges.BoughtUsed = SchemeDirector.BoughtUsed;
                Justice.Bridges.FileLines = SchemeDirector.FileLines;
                Justice.Bridges.FileActs = SchemeDirector.FileActs;
                _takingsBefore = EconomyBridges.Takings;
                EconomyBridges.Takings = Takings;
                _paperBefore = News.Extra;
                News.Extra = Pieces;
            }

            internal static void Disconnect()
            {
                SchemeBridges.Reset();
                CompanyBridges.QuietLabel = null;
                CompanyBridges.Quiet = null;
                ServicesBridges.BooksLift = null;
                ServicesBridges.SeesThrough = null;
                ServicesBridges.Sold = _soldBefore;   // the stock market's own takes its off next
                _soldBefore = null;
                SocialPhone.QuietLabel = null;
                SocialPhone.QuietAsk = null;
                NetworkBridges.Bought = null;
                NetworkBridges.BoughtUsed = null;
                Justice.Bridges.FileLines = null;
                Justice.Bridges.FileActs = null;
                EconomyBridges.Takings = _takingsBefore;
                _takingsBefore = null;
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            // The player's businesses as the company last read them, each with the covers in force and whether cash crosses its counter.
            private static void Shops(List<SchemeShopInfo> into)
            {
                try
                {
                    CompanyDirector c = CompanyDirector.Instance;
                    IReadOnlyList<CompanyShop> shops = c != null ? c.Shops : null;
                    for (int i = 0; shops != null && i < shops.Count; i++)
                    {
                        CompanyShop shop = shops[i];
                        int covers = 0;
                        for (int k = 0; c.On(CompanyPart.Cover) && k < shop.File.Cover.Length; k++)
                        {
                            covers |= CoverRules.Covers(shop.File.Cover[k], c.Today) ? 1 << k : 0;
                        }
                        into.Add(new SchemeShopInfo
                        {
                            Place = shop.File.Place, Name = shop.Name, District = shop.District, Average = shop.Average, Cash = c.Promoted(shop), Covers = covers,
                            Mine = shop.Reg == null || GoingPublic.Mine(shop.Reg)
                        });
                    }
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.Shops", e);
                }
            }

            private static bool Claim(string place, int cover, int loss, out int id, out int payout, out int payDay)
            {
                id = payout = payDay = 0;
                try
                {
                    return CompanyDirector.Instance != null && CompanyDirector.Instance.InventClaim(place, cover, loss, out id, out payout, out payDay);
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.Claim", e);
                    return false;
                }
            }

            private static bool Visit(string place)
            {
                try
                {
                    CompanyShop shop = CompanyDirector.Instance != null ? CompanyDirector.Instance.ShopAt(place) : null;
                    return shop != null && shop.Reg != null && CompanyBridges.Audit != null && CompanyBridges.Audit(shop.Reg);
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.Visit", e);
                    return false;
                }
            }

            // A day's washed cash as the business's takings of the day that ended: in its statement, so the game's tax counts it as sales.
            private static bool Till(string place, long net, int day)
            {
                try
                {
                    CompanyShop shop = CompanyDirector.Instance != null ? CompanyDirector.Instance.ShopAt(place) : null;
                    if (shop == null || ReferenceEquals(shop.Address, null) || net <= 0 || (shop.Reg != null && !GoingPublic.Mine(shop.Reg)))
                    {
                        return false;
                    }
                    if (!BizBooks.Book(net, new TransactionInfo(SchemeGame.WashKey), day - 1, shop.Address))
                    {
                        return false;
                    }
                    GameBooks.Refresh(day - 1);
                    return true;
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.Till", e);
                    return false;
                }
            }

            private static bool Takings(string type)
            {
                return type == SchemeGame.WashKey || (_takingsBefore != null && _takingsBefore(type));
            }

            private static bool Club(string district, int club, int households, int level, out long taken, out int joined)
            {
                taken = 0;
                joined = 0;
                try
                {
                    PopulationState s = CityPopulation.Running && !CityPopulation.Following ? CityPopulation.Live : null;
                    SchemeState state = SchemeDirector.Instance != null ? SchemeDirector.Instance.State : null;
                    int at = s != null ? s.DistrictOf(district) : -1;
                    if (at < 0 || state == null || GoingPublic.InSession)
                    {
                        return false;
                    }
                    int seed = state.Seed;
                    taken = PurseRules.Take(s, at, households, id => SchemeRules.ClubDraw(seed, club, id), savings => SchemeRules.ClubStake(savings, level), out joined);
                    return taken > 0;
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.Club", e);
                    return false;
                }
            }

            private static long Restore(string place, long amount)
            {
                try
                {
                    PopulationState s = CityPopulation.Running && !CityPopulation.Following ? CityPopulation.Live : null;
                    if (s == null || GoingPublic.InSession)
                    {
                        return 0L;
                    }
                    CompanyShop shop = CompanyDirector.Instance != null ? CompanyDirector.Instance.ShopAt(place) : null;
                    return PurseRules.Give(s, shop != null ? s.DistrictOf(shop.District) : -1, amount);
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.Restore", e);
                    return 0L;
                }
            }

            // How the courts ended a case, as the schemes count it: 0 while it is open.
            private static int CaseEnd(int number)
            {
                int outcome = SchemeCases.Outcome(number);
                if (outcome == 0)
                {
                    return 0;
                }
                switch (outcome)
                {
                    case (int)CaseOutcome.Convicted:
                    case (int)CaseOutcome.Guilty:
                    case (int)CaseOutcome.Deal:
                        return (int)InquiryEnd.Convicted;
                    case (int)CaseOutcome.Settled:
                        return (int)InquiryEnd.Settled;
                    case -1:
                        return (int)InquiryEnd.Dropped;   // the courts no longer hold it
                    default:
                        return (int)InquiryEnd.Cleared;
                }
            }

            // The council's seats: each a name that changes when the seat changes hands at an election, so a new councillor has a temper of their own.
            private static void Council(List<string> into)
            {
                try
                {
                    CivicDirector d = CivicDirector.Instance;
                    GovState g = d != null && d.Government != null ? d.Government.State : null;
                    for (int i = 0; g != null && i < g.Council.Length; i++)
                    {
                        into.Add("seat " + i + " party " + g.Council[i] + " term " + g.Number);
                    }
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.Council", e);
                }
            }

            private static int RoleOf(int person)
            {
                try
                {
                    NetContact c = NetworkDirector.On ? NetworkDirector.Of(person) : null;
                    return c != null ? (int)c.Role : 0;
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.RoleOf", e);
                    return 0;
                }
            }

            private static void Cooled(int person)
            {
                try
                {
                    SocialPerson p = SocialState.Loaded ? SocialState.Get(person) : null;
                    if (p != null)
                    {
                        SocialLife.Change(p, 0f, -3f, -8f);
                    }
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.Cooled", e);
                }
            }

            private static RivalFile RivalsFile()
            {
                RivalDirector d = RivalDirector.Instance;
                return d != null && !d.Resting ? d.File : null;
            }

            private static void Rivals(SchemeWorld w)
            {
                try
                {
                    RivalFile f = RivalsFile();
                    for (int i = 0; f != null && i < f.Tycoons.Length && i < w.RivalActive.Length; i++)
                    {
                        w.RivalActive[i] = f.Tycoons[i].Active;
                        w.RivalWorth[i] = f.Tycoons[i].Worth;
                    }
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.Rivals", e);
                }
            }

            private static void RivalMoney(int tycoon, long delta)
            {
                try
                {
                    RivalFile f = RivalsFile();
                    if (f != null && tycoon >= 0 && tycoon < f.Tycoons.Length && f.Tycoons[tycoon].Active)
                    {
                        f.Tycoons[tycoon].Worth = Math.Max(0L, f.Tycoons[tycoon].Worth + delta);
                    }
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.RivalMoney", e);
                }
            }

            private static void RivalBarred(int tycoon, int barDays, int quietDays)
            {
                try
                {
                    RivalFile f = RivalsFile();
                    if (f == null || tycoon < 0 || tycoon >= f.Tycoons.Length)
                    {
                        return;
                    }
                    int day = TimeHelper.CurrentDay;
                    Tycoon t = f.Tycoons[tycoon];
                    t.BarredUntil = Math.Max(t.BarredUntil, day + barDays);
                    t.SlowUntil = Math.Max(t.SlowUntil, day + barDays);
                    t.QuietUntil = Math.Max(t.QuietUntil, day + quietDays);
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.RivalBarred", e);
                }
            }

            private static void Sold(string place, int price)
            {
                try
                {
                    _soldBefore?.Invoke(place, price);
                }
                finally
                {
                    SchemeDirector.SaleDone(place, price);
                }
            }

            // The rivals' pieces for a morning's paper, after the others': read from the save's state only, so the same day gives the same pieces.
            private static void Pieces(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.Pieces (the others')", e);
                }
                try
                {
                    SchemeState s = SchemeDirector.Instance != null ? SchemeDirector.Instance.State : null;
                    for (int i = 0; s != null && into != null && i < s.Rival.Length; i++)
                    {
                        SchemeRival r = s.Rival[i];
                        if (r.NewsDay != day - 1 || r.News < SchemeRules.RivalNewsLetter || r.News > SchemeRules.RivalNewsCleared)
                        {
                            continue;
                        }
                        TycoonDef def = RivalCatalogue.Tycoons[RivalCatalogue.Clamp(i, 0, RivalCatalogue.TycoonCount - 1)];
                        into.Add(new NewsItem
                        {
                            Key = SchemeText.RivalPaperKeys[r.News - 1],
                            Data = SchemeGame.Data("rival", def.Name, "holding", def.Holding, "what", SchemeText.Kind(r.NewsKind), "amount", SchemeText.Money(r.NewsAmount))
                        });
                    }
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.Pieces", e);
                }
            }

            // The call's button for a quiet word: only for somebody with a position who has not said no, with something to be had today.
            private static string PersonLabel(SocialPerson p)
            {
                try
                {
                    if (p == null || !p.Met)
                    {
                        return null;
                    }
                    SchemeDirector.Favours(p.Index, Asked);
                    return Asked.Count > 0 ? "reallife_scheme_btn_word" : null;
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.PersonLabel", e);
                    return null;
                }
            }

            private static DialogEntry PersonAsk(SocialPerson p)
            {
                return Entry(p, 0);
            }

            // One favour at a time: what it is, what it costs, how it would be paid and what it risks; then the next, or the money.
            private static DialogEntry Entry(SocialPerson p, int at)
            {
                Dictionary<string, string> data = SocialLife.Data(p);
                try
                {
                    SchemeDirector.Favours(p.Index, Asked);
                    if (Asked.Count == 0)
                    {
                        return PhoneContacts.Closing("reallife_scheme_talk_not_now", data);
                    }
                    Favour favour = Asked[at % Asked.Count];
                    long price = SchemeDirector.PriceOf(favour, out bool cash);
                    data["amount"] = SchemeText.Money(price);
                    data["way"] = SchemeText.T(cash ? "reallife_scheme_way_cash" : "reallife_scheme_way_account");
                    var entry = new DialogEntry { messageData = SchemeText.FavourAskKeys[(int)favour].Localize(data), OnCancel = PhoneContacts.HangUp };
                    entry.ConfirmTextOverride = SchemeText.FavourKeys[(int)favour].Localize(data);
                    entry.OnConfirm = () => Answer(p, favour);
                    entry.Pays(DialogButton.Confirm, "reallife_ui_dialog_cost_now", "amount", SchemeText.Money(price));
                    if (Asked.Count > 1)
                    {
                        int next = at + 1;
                        entry.SecondOptionTextOverride = "reallife_social_btn_else";
                        entry.OnSecondOption = () => Entry(p, next);
                    }
                    return entry;
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.Entry", e);
                    return PhoneContacts.Closing("reallife_scheme_talk_not_now", data);
                }
            }

            // The money offered: what they say, as the call closes.
            private static DialogEntry Answer(SocialPerson p, Favour favour)
            {
                string key = "reallife_scheme_talk_not_now";
                Dictionary<string, string> data = SocialLife.Data(p);
                try
                {
                    FavourAnswer answer = SchemeDirector.Offer(p.Index, favour);
                    key = answer.Key ?? key;
                    for (int i = 0; answer.Pairs != null && i + 1 < answer.Pairs.Length; i += 2)
                    {
                        data[answer.Pairs[i]] = answer.Pairs[i + 1] ?? string.Empty;
                    }
                    data["standing"] = answer.Words ?? string.Empty;
                    SocialLife.Contacted(p);
                }
                catch (Exception e)
                {
                    Fault("SchemeLinks.Answer", e);
                }
                SocialLife.Said(p, key, data);
                return PhoneContacts.Closing(key, data);
            }
        }
    }
}
