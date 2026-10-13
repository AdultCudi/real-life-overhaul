using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The City tab and the accounts: the city's influencers, the brands and the candidates, each with a profile and a
    /// Follow button (a new game follows nobody; following is what lets an account notify, once a day at most); and on
    /// the Business tab, hiring influencers for a business and the deals made. The box does not scroll, so a profile is
    /// a handful of lines.
    /// </summary>
    public sealed partial class FlareApp
    {
        private readonly List<FlareInfluencer> _active = new List<FlareInfluencer>(FlareCast.Most);
        private bool _hireOpen;      // the shop's page shows the hiring page
        private int _hireWho;        // which of the active influencers is picked for the deal
        private int _hireDeal;       // which deal

        // Who wrote a post, as the Feed names them: you, a friend, an influencer's handle, a brand, a candidate.
        private string AuthorName(FlarePost p, FlareFile file)
        {
            switch (p.Author)
            {
                case FlareAuthor.Player: return FlareText.T("reallife_flare_you");
                case FlareAuthor.Friend: return _director.PersonName(p.Who) ?? "?";
                case FlareAuthor.Influencer:
                    FlareInfluencer inf = file.Influencer(p.Who);
                    return inf == null ? "?" : inf.Person >= 0 && _director.PersonName(inf.Person) != null ? FlareText.Fill("reallife_flare_handle_known", "handle", inf.Handle, "name", _director.PersonName(inf.Person)) : inf.Handle;
                case FlareAuthor.Ledger: return FlareText.T("reallife_flare_ledger_name");
                default: return p.Name ?? "?";
            }
        }

        // ---------------------------------------------------------------- the City tab

        private TableSpec WriteCity(InfoBox box, FlareFile file, int day, ref string selected)
        {
            string picked = null;
            for (int i = 0; i < file.Influencers.Count; i++)
            {
                FlareInfluencer inf = file.Influencers[i];
                if (!inf.Active)
                {
                    continue;
                }
                string key = "i:" + inf.Id;
                TableRow row = Row("acc:" + key);
                row.Kind = FlareTables.IsInfluencer;
                row.Set(0, inf.Handle + (inf.Star ? " " + FlareText.T("reallife_flare_star_mark") : string.Empty));
                row.Set(1, FlareText.Niche(inf.Niche));
                row.Set(2, FlareText.N(inf.Followers), inf.Followers);
                row.Set(3, FlareGame.DistrictName(inf.District));
                row.Set(4, FlareCast.Followed(file, key) ? FlareText.T("reallife_flare_following") : string.Empty);
                picked = Is(row, ref selected) ? key : picked;
            }
            IReadOnlyList<string> brands = _director.BrandAccounts, candidates = _director.CandidateAccounts;
            for (int i = 0; i < brands.Count + candidates.Count; i++)
            {
                bool brand = i < brands.Count;
                string name = brand ? brands[i] : candidates[i - brands.Count];
                string key = (brand ? "b:" : "c:") + name;
                TableRow row = Row("acc:" + key);
                row.Kind = brand ? FlareTables.IsBrand : FlareTables.IsCandidate;
                row.Set(0, name);
                row.Set(1, FlareText.T(brand ? "reallife_flare_account_brand" : "reallife_flare_account_candidate"));
                row.Set(4, FlareCast.Followed(file, key) ? FlareText.T("reallife_flare_following") : string.Empty);
                picked = Is(row, ref selected) ? key : picked;
            }
            if (picked != null)
            {
                WriteAccount(box, file, picked, day);
                return FlareTables.City;
            }
            box.Title(FlareText.T("reallife_flare_tab_city"));
            box.Line(FlareText.Fill("reallife_flare_city_count", "count", file.ActiveInfluencers.ToString(), "most", FlareCast.Most.ToString()));
            box.Line(FlareText.T("reallife_flare_city_intro"));
            box.Line(FlareText.T("reallife_flare_city_follow"));
            Meta.Guide.Point(box, GuideTitle);
            return FlareTables.City;
        }

        // An account's profile: who they are and the Follow button.
        private void WriteAccount(InfoBox box, FlareFile file, string key, int day)
        {
            FlareInfluencer inf = key.StartsWith("i:", System.StringComparison.Ordinal) ? file.Influencer(Number(key.Substring(2))) : null;
            if (inf != null)
            {
                box.Title(inf.Handle);
                if (day - inf.ScandalDay <= FlareCast.MonthDays)
                {
                    box.Line(FlareText.T("reallife_flare_profile_scandal"));
                }
                box.Line(FlareText.Fill("reallife_flare_profile_what", "niche", FlareText.Niche(inf.Niche), "district", FlareGame.DistrictName(inf.District), "style", FlareText.Style(inf.Style)));
                box.Line(FlareText.Fill("reallife_flare_profile_figures", "followers", FlareText.N(inf.Followers), "engaged", (inf.Engagement / 100f).ToString("0.0") + "%",
                    "reliable", inf.Reliability.ToString()));
                box.Line(FlareText.Pair("reallife_flare_profile_price", FlareText.Money(Prices.At(FlareCast.Price(inf)))));
                if (inf.Person >= 0 && _director.PersonName(inf.Person) != null)
                {
                    box.Line(FlareText.Fill(inf.Star ? "reallife_flare_profile_star_person" : "reallife_flare_profile_friend", "name", _director.PersonName(inf.Person)));
                }
                else if (inf.Staff)
                {
                    box.Line(FlareText.T("reallife_flare_profile_staff"));
                }
            }
            else
            {
                box.Title(key.Substring(2));
                box.Line(FlareText.T(key.StartsWith("b:", System.StringComparison.Ordinal) ? "reallife_flare_account_brand_about" : "reallife_flare_account_candidate_about"));
            }
            bool following = FlareCast.Followed(file, key);
            box.Line(FlareText.T(following ? "reallife_flare_profile_followed" : "reallife_flare_profile_not_followed"));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, following ? "reallife_flare_unfollow" : "reallife_flare_follow", () => _director.Follow(key, !following));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_flare_back", Back);
            Meta.Guide.Point(box, GuideTitle);
        }

        private static int Number(string text)
        {
            int n;
            return int.TryParse(text, out n) ? n : -1;
        }

        // ---------------------------------------------------------------- hiring, on the Business tab

        // The deals made, as rows under the businesses.
        private void HireRows(FlareFile file, int day, ref string selected, ref FlareHire picked)
        {
            for (int i = file.Hires.Count - 1; i >= 0; i--)
            {
                FlareHire h = file.Hires[i];
                TableRow row = Row("hire:" + h.Id);
                row.Set(0, FlareText.Fill("reallife_flare_row_hire", "business", h.Name, "who", _director.WhoLine(h)));
                row.Set(1, FlareText.Deal(h.Kind - FlareCast.FirstDealKind));
                row.Set(2, h.Ended ? FlareText.T("reallife_flare_hire_over") : FlareText.Fill("reallife_flare_hire_percent", "percent", h.Percent.ToString()), h.Percent);
                row.Set(4, FlareText.Fill("reallife_flare_day", "day", h.To.ToString()), h.To);
                picked = Is(row, ref selected) ? h : picked;
            }
        }

        private void WriteHireRow(InfoBox box, FlareFile file, FlareHire h, int day)
        {
            box.Title(FlareText.Fill("reallife_flare_row_hire", "business", h.Name, "who", _director.WhoLine(h)));
            if (h.Ended)
            {
                box.Line(FlareText.T("reallife_flare_hire_ended"));
            }
            if (h.Late)
            {
                box.Line(FlareText.T(day >= h.From ? "reallife_flare_hire_late" : "reallife_flare_hire_unknown"));
            }
            if (h.OffMessage && day >= h.From)
            {
                box.Line(FlareText.T("reallife_flare_hire_off"));
            }
            if (h.Backfired && day >= h.From)
            {
                box.Line(FlareText.T("reallife_flare_hire_backfired"));
            }
            bool scandal = false;
            for (int i = 0; i < h.Who.Count; i++)
            {
                FlareInfluencer inf = file.Influencer(h.Who[i]);
                scandal |= inf != null && inf.ScandalDay >= h.From && inf.ScandalDay <= h.To;
            }
            if (scandal)
            {
                box.Line(FlareText.T("reallife_flare_hire_scandal"));
            }
            box.Line(FlareText.Pair("reallife_flare_hire_deal", FlareText.Deal(h.Kind - FlareCast.FirstDealKind)));
            box.Line(FlareText.Fill("reallife_flare_hire_days", "from", h.From.ToString(), "to", h.To.ToString(), "percent", h.Percent.ToString()));
            Transform buttons = box.ButtonRow();
            if (!h.Ended)
            {
                int id = h.Id;
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_flare_hire_end", () => _director.EndHire(id));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_flare_back", Back);
            Meta.Guide.Point(box, GuideTitle);
        }

        // The hiring page of a business: who, which deal, what the company would charge and bring, and the Hire button.
        private void WriteHire(InfoBox box, FlareFile file, string place, string name, int day)
        {
            box.Title(FlareText.Fill("reallife_flare_hire_title", "business", name));
            _active.Clear();
            for (int i = 0; i < file.Influencers.Count; i++)
            {
                if (file.Influencers[i].Active)
                {
                    _active.Add(file.Influencers[i]);
                }
            }
            int deal = FlareRules.Clamp(_hireDeal, 0, FlareText.DealKeys.Length - 1);
            Choose(box, FlareText.DealKeys.Length, deal, i => FlareText.Pair("reallife_flare_hire_deal", FlareText.Deal(i)), i => _hireDeal = i);
            int who = -1;
            if ((FlareDeal)deal != FlareDeal.Plan && _active.Count > 0)
            {
                int at = FlareRules.Clamp(_hireWho, 0, _active.Count - 1);
                Choose(box, _active.Count, at, i => FlareText.Pair("reallife_flare_hire_who", _active[i].Handle + ", " + FlareText.Niche(_active[i].Niche)), i => _hireWho = i);
                who = _active[at].Id;
            }
            int cost, percent;
            string why;
            bool can = _director.HireQuote(place, (FlareDeal)deal, who, out cost, out percent, out why);
            box.Line(can ? FlareText.Fill("reallife_flare_hire_quote", "cost", FlareText.Money(cost), "percent", percent.ToString()) : FlareText.T(why ?? "reallife_flare_hire_no_one"));
            Transform buttons = box.ButtonRow();
            Button hire = InfoBox.Button(buttons, InfoBox.Positive, "reallife_flare_hire_button", () =>
            {
                if (_director.Hire(place, (FlareDeal)deal, who))
                {
                    _hireOpen = false;
                    Refresh();
                }
            });
            if (hire != null && !can)
            {
                hire.interactable = false;
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_flare_back", () =>
            {
                _hireOpen = false;
                Refresh();
            });
            box.Line(FlareText.T(FlareText.DealAboutKeys[deal]));
            Meta.Guide.Point(box, GuideTitle);
        }
    }
}
