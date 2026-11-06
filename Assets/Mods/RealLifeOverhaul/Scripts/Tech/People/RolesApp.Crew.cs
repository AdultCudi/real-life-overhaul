using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The Search tab's data centre crew: a search firm briefed from the player's headquarters for so many technicians or network
    /// engineers at one data centre, junior to staff level. Reached from its pinned row or from a data centre's page on the Tech
    /// app's Infra tab (<see cref="TechDirector.BriefCrew"/>). Its candidates are interviewed and hired as any applicant's.
    /// </summary>
    public sealed partial class RolesApp
    {
        public const string NewCrewRow = "newcrew";

        private string _crewPlace;          // the data centre chosen; null for the first
        private int _crewRole, _crewLevel;  // an index of HiringCatalogue.CrewRoles, and a level
        private int _crewCount;             // technicians asked for; 0 for what the centre lacks

        /// <summary>The crew page opens on this data centre, asking for what it lacks.</summary>
        internal void WantCrew(string place)
        {
            _crewPlace = place;
            _crewCount = 0;
        }

        private void WriteNewCrew(InfoBox box)
        {
            InfraFile infra = _director.Infra;
            box.Title(TechText.T("reallife_crew_new_title"));
            if (infra == null || infra.Centres.Count == 0)
            {
                box.Line(TechText.T("reallife_crew_no_centres"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            int at = 0;
            for (int i = 0; i < infra.Centres.Count; i++)
            {
                at = infra.Centres[i].Place == _crewPlace ? i : at;
            }
            DataCentre d = infra.Centres[at];
            _crewPlace = d.Place;
            _crewRole = Mathf.Clamp(_crewRole, 0, HiringCatalogue.CrewRoles.Length - 1);
            int role = HiringCatalogue.CrewRoles[_crewRole];
            int least = HiringRules.CrewLevel(role, HiringCatalogue.CrewLeastLevel), most = HiringRules.CrewLevel(role, HiringCatalogue.CrewMostLevel);
            _crewLevel = HiringRules.CrewLevel(role, _crewLevel);
            int count = _crewCount > 0 ? _crewCount : InfraRules.CrewLacking(d);
            count = TalentCatalogue.Clamp(count, 1, HiringCatalogue.CrewMost);
            if (box.HasSelector)
            {
                box.Selector(infra.Centres.Count, at, i => infra.Centres[i].Name, i => Set(() =>
                {
                    _crewPlace = infra.Centres[i].Place;
                    _crewCount = 0;   // another centre: what it lacks
                }));
                box.Selector(HiringCatalogue.CrewRoles.Length, _crewRole, i => TechText.Role(HiringCatalogue.CrewRoles[i]), i => Set(() => _crewRole = i));
                box.Selector(most - least + 1, _crewLevel - least, i => TechText.Level(least + i), i => Set(() => _crewLevel = least + i));
                box.Selector(HiringCatalogue.CrewMost, count - 1, i => TechText.T("reallife_crew_count", "count", (i + 1).ToString()), i => Set(() => _crewCount = i + 1));
            }
            box.Line(TechText.Fill("reallife_crew_centre", "have", d.Technicians.ToString(), "need", InfraRules.TechniciansNeeded(d).ToString(), "racks", (d.Racks + d.GpuRacks).ToString()));
            BuildingRegistration hq = HiringGame.Headquarters();
            _director.CrewQuote(role, _crewLevel, count, out int fee, out int retainer, out int each);
            box.Line(TechText.Fill("reallife_crew_cost", "each", TechText.Cents(each), "fee", TechText.Cents(fee), "retainer", TechText.Cents(retainer),
                "count", HiringRules.CrewCandidates(count).ToString(), "least", HiringCatalogue.CrewDaysLeast.ToString(), "most", HiringCatalogue.CrewDaysMost.ToString()));
            string refusal = _director.CrewRefusal(d.Place);
            if (hq != null && refusal == null)
            {
                box.Line(TechText.T("reallife_crew_from_hq", "hq", TalentGame.NameOf(hq)));
            }
            else if (refusal != null)
            {
                box.Line(TechText.T(refusal), CellTone.Red);
            }
            Transform buttons = box.ButtonRow();
            if (refusal == null)
            {
                string place = d.Place;
                int level = _crewLevel, asked = count;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_hire_button_brief", () => Do(() =>
                {
                    if (_director.StartCrewSearch(place, role, level, asked))
                    {
                        _picked = null;
                        _crewCount = 0;
                    }
                }));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            box.Note(TechText.Fill("reallife_crew_about", "least", TechText.Bp(HiringCatalogue.CrewFeeBp[0]), "most", TechText.Bp(HiringCatalogue.CrewFeeBp[HiringCatalogue.CrewFeeBp.Length - 1])));
            Meta.Guide.Point(box, JobsGuide);
        }

        private void WriteCrewSearch(InfoBox box, JobAd ad)
        {
            BuildingRegistration centre = TalentGame.Find(ad.Place);
            box.Title(TechText.T("reallife_crew_search_title", "centre", centre != null ? TalentGame.NameOf(centre) : "-"));
            box.Line(TechText.Fill("reallife_crew_terms", "role", TechText.Role(ad.Role), "level", TechText.Level(ad.Level), "count", ad.Crew.ToString(), "hired", ad.Hired.ToString(),
                "fee", TechText.Cents(ad.Fee), "paid", TechText.Cents(ad.FeePaid), "day", ad.Due.ToString()));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_hire_button_applicants", () => ShowTab(TechDirector.TabApplicants));
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, JobsGuide);
        }
    }
}
