using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The run's fund and its street work: money in and out, doors, posters, the player's rally and the debate.</summary>
    public sealed partial class CivicOffice
    {
        private readonly HashSet<int> _knocked = new HashSet<int>();
        private int _knockedDay = -1;
        private const int KnockedMost = 200;   // doors a day

        /// <summary>A step of the player's own money into the fund. Returns the locale key.</summary>
        public string PutIn()
        {
            if (!State.Running)
            {
                return "reallife_civic_office_no_run";
            }
            int step = Prices.At(OfficeRules.OwnStep);
            if (!GameManager.ChangeMoneySafe(-step, new TransactionInfo("reallife_transaction_civic_office_own"), null, null, force: false, showNotification: true))
            {
                return "reallife_civic_office_no_money";
            }
            State.Fund += step;
            State.Own += step;
            State.Raised += step;
            return "reallife_civic_office_put_in";
        }

        /// <summary>The donor's offer is taken. Against the law it is money that can come out later.</summary>
        public string Accept(int day)
        {
            if (State.OfferKind < 0 || State.OfferAmount <= 0)
            {
                return "reallife_civic_office_no_offer";
            }
            int amount = State.OfferAmount;
            bool legal = OfficeRules.Legal(State.OfferKind, amount, Limit);
            State.Fund += amount;
            State.Raised += amount;
            if (!legal)
            {
                State.Dirty += amount;
                State.DirtyDay = day;
                Logger("money taken against the law: " + amount + " (" + State.OfferKind + "); " + State.Dirty + " that could come out.");
            }
            State.OfferKind = -1;
            State.OfferAmount = 0;
            return legal ? "reallife_civic_office_offer_taken" : "reallife_civic_office_offer_taken_dirty";
        }

        public string Decline()
        {
            State.OfferKind = -1;
            State.OfferAmount = 0;
            return "reallife_civic_office_offer_declined";
        }

        public const int BuyPosters = 0, BuyLedger = 1, BuyFlare = 2, BuyRally = 3, BuyCanvassers = 4;

        public static int Price(int what)
        {
            switch (what)
            {
                case BuyPosters: return Prices.At(OfficeRules.PosterRun);
                case BuyLedger: return Prices.At(OfficeRules.LedgerAd);
                case BuyFlare: return Prices.At(OfficeRules.FlareAd);
                case BuyRally: return Prices.At(OfficeRules.RallyHire);
                default: return Prices.At(OfficeRules.Canvassers);
            }
        }

        /// <summary>Spends from the fund. A rally is staged by the caller (CampaignWorld) once this has said yes. Returns the locale key.</summary>
        public string Buy(int what, int day, int hour)
        {
            if (!Campaigning)
            {
                return "reallife_civic_office_no_campaign";
            }
            int price = Price(what);
            if (State.Fund < price)
            {
                return "reallife_civic_office_fund_short";
            }
            switch (what)
            {
                case BuyPosters:
                    State.PostersHeld += OfficeRules.PostersPerRun;
                    break;
                case BuyLedger:
                case BuyFlare:
                    State.Push += (what == BuyLedger ? 0.03f : 0.025f) * Mathf.Pow(0.8f, State.Ads);
                    State.Ads++;
                    if (what == BuyLedger)
                    {
                        State.Story(day + 1, "reallife_civic_news_office_advert", CivicText.PlayerName(), CivicText.T(CivicCatalogue.Slogans[State.Me.Slogan]));
                    }
                    else
                    {
                        Post(PostFiled, CivicText.T(CivicCatalogue.Slogans[State.Me.Slogan]));
                    }
                    break;
                case BuyRally:
                    if (State.LastRally == day)
                    {
                        return "reallife_civic_office_rally_today";
                    }
                    if (hour < OfficeRules.RallyFrom || hour >= OfficeRules.RallyTo)
                    {
                        return "reallife_civic_office_rally_hours";
                    }
                    break;
                default:
                    if (State.CanvassDay == day)
                    {
                        return "reallife_civic_office_canvass_today";
                    }
                    State.CanvassDay = day;
                    Doors(RaceDistrict(), OfficeRules.CanvasserDoors, OfficeRules.CanvasserDoors * 0.0015f);
                    break;
            }
            State.Fund -= price;
            State.Spent += price;
            return what == BuyPosters ? "reallife_civic_office_bought_posters" : what == BuyRally ? "reallife_civic_office_bought_rally"
                : what == BuyCanvassers ? "reallife_civic_office_bought_canvassers" : "reallife_civic_office_bought_advert";
        }

        /// <summary>The rally's money back: no place for it could be found.</summary>
        public void RallyRefund()
        {
            int price = Price(BuyRally);
            State.Fund += price;
            State.Spent = Math.Max(0, State.Spent - price);
        }

        // Doors knocked in a district (by the player or canvassers): support there, a little more of it turning out.
        private void Doors(int district, int doors, float push)
        {
            State.Doors += doors;
            if (district >= 0 && district < State.DistrictPush.Length)
            {
                State.DistrictDoors[district] += doors;
                State.DistrictPush[district] = Mathf.Clamp(State.DistrictPush[district] + push, -0.2f, OfficeRules.MostPush);
            }
            else
            {
                State.Push += push * 0.5f;
            }
        }

        /// <summary>True if the player may knock on doors now (and at this building, of this district): a run on, the hours, not twice a day.</summary>
        public bool MayKnock(int day, int hour, string neighbourhood, int building)
        {
            if (!Enabled || !State.Running || (State.Filed && !Campaigning) || hour < OfficeRules.DoorsFrom || hour >= OfficeRules.DoorsTo)
            {
                return false;
            }
            if (State.Race == OfficeRace.Council && neighbourhood != State.District)
            {
                return false;
            }
            if (_knockedDay != day)
            {
                _knockedDay = day;
                _knocked.Clear();
            }
            return !_knocked.Contains(building) && _knocked.Count < KnockedMost;
        }

        public void Knocked(int building, string neighbourhood, OfficeGain gain, int day)
        {
            _knocked.Add(building);
            State.LastDoorDay = day;
            Doors(DistrictIndex(neighbourhood), 1, gain.Push);
            if (Collecting)
            {
                State.Signatures += gain.Signatures;
            }
            if (gain.Donation > 0)
            {
                int amount = Prices.At(gain.Donation);
                State.Fund += amount;
                State.Raised += amount;
            }
        }

        public void PosterUp(string neighbourhood)
        {
            if (State.PostersHeld <= 0)
            {
                return;
            }
            State.PostersHeld--;
            State.PostersUp++;
            if (State.PostersUp * OfficeRules.PosterPush <= OfficeRules.PosterPushMost)
            {
                int d = DistrictIndex(neighbourhood);
                if (d >= 0)
                {
                    State.DistrictPush[d] = Mathf.Min(OfficeRules.MostPush, State.DistrictPush[d] + OfficeRules.PosterPush);
                }
                else
                {
                    State.Push += OfficeRules.PosterPush * 0.5f;
                }
            }
        }

        public void Rallied(OfficeGain gain, int day)
        {
            State.Rallies++;
            State.LastRally = day;
            State.Push += gain.Push;
            CivicBridges.Moved(0, 3);
            Post(PostRally, Where());
        }

        public void Debated(float gain, bool there, int day)
        {
            State.Debate = there ? 1 : 2;
            State.DebateGain = gain;
            State.Push += gain;
            if (there)
            {
                CivicBridges.Moved(0, 8);
                Post(PostDebate, Where());
            }
            State.Story(day + 1, there ? (gain >= 0.04f ? "reallife_civic_news_office_debate_good" : gain < 0f ? "reallife_civic_news_office_debate_bad" : "reallife_civic_news_office_debate_even")
                : "reallife_civic_news_office_debate_absent", CivicText.PlayerName(), Where());
        }
    }
}
