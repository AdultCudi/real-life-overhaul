using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class TechLive
    {
        /// <summary>
        /// What the Tech app's overview shows of a firm and the firm's own page (StudioPageScreen) does not carry: the game or product in
        /// development, the people with their skill and morale, and the support tickets unanswered. Written by the overview's own code, so
        /// the two never differ. Null when the firm or the save cannot be read. Cost: once when the page opens or after a press.
        /// </summary>
        internal static StudioExtras Extras(string place)
        {
            TechDirector d = TechDirector.Instance;
            FirmPlace firm = d != null ? d.FirmOf(place) : null;
            if (d == null || firm == null || d.Firms == null || SaveGameManager.Current == null)
            {
                return null;
            }
            return new Page(null, d, d.Firms, new TechView(), new TechModel()).Extras(firm);
        }

        private sealed partial class Page
        {
            internal StudioExtras Extras(FirmPlace firm)
            {
                _firm = firm;
                string place = firm.File.Place;
                var people = PeopleOf(place);
                var extras = new StudioExtras { Hero = HeroOf(place, Studio), People = new UiPersonRow[people.Count] };
                for (int i = 0; i < people.Count; i++)
                {
                    extras.People[i] = PersonRow(people[i]);
                }
                StudioPlace sp = Studio ? _d.StudioPlaceOf(firm) : null;
                if (sp != null && sp.Tickets > sp.Answered)
                {
                    extras.Tickets = TechText.T("reallife_ui_tech_note_support", "tickets", TechText.Whole(sp.Tickets - sp.Answered));
                }
                return extras;
            }
        }
    }
}
