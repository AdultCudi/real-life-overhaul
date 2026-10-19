using System.Collections.Generic;
using System.Text;
using Dialogs;
using Entities;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The "People" page: everybody the player knows, closest first, how things stand with each, and what they would like to do.</summary>
    public sealed class PeopleDialog : Dialog
    {
        // By origin: where somebody not yet met may be found.
        private static readonly string[] Hints =
        {
            null, null, "reallife_social_hint_gym", "reallife_social_hint_work", "reallife_social_hint_cart", "reallife_social_hint_neighbour",
            "reallife_social_hint_helped", "reallife_social_hint_customer"
        };

        private readonly List<SocialPerson> _known = new List<SocialPerson>(SocialRules.CastSize);

        public PeopleDialog()
        {
            npcNameKey = SocialPhone.PeopleId;
            if (!SocialState.Loaded || !Settings.Social)
            {
                DialogController.current.ShowEntry(Guard.Entry(PhoneContacts.Closing("reallife_social_call_off")));
                return;
            }
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && p.Met)
                {
                    _known.Add(p);
                }
            }
            _known.Sort((a, b) => b.Closeness.CompareTo(a.Closeness));
            DialogController.current.ShowEntry(Guard.Entry(Overview()));
        }

        private DialogEntry Overview()
        {
            var text = new StringBuilder(256);
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && !p.Met && Hints[i] != null)
                {
                    text.Append(text.Length > 0 ? ", " : string.Empty).Append(SocialLife.T(Hints[i]));
                }
            }
            SocialPlan plan = SocialState.Plan;
            SocialPerson with = SocialState.Get(plan.Person);
            string planLine = plan.Active && with != null
                ? SocialLife.T("reallife_social_people_plan", new Dictionary<string, string>
                {
                    { "name", with.Name }, { "outing", SocialLife.T(SocialCatalogue.Outing(plan.Kind).Name) }, { "place", plan.Place ?? string.Empty },
                    { "time", SocialLife.Clock(plan.Time) }
                })
                : SocialLife.T("reallife_social_people_noplan");
            var data = new Dictionary<string, string>
            {
                { "count", _known.Count.ToString() },
                { "plan", planLine },
                { "hints", text.Length > 0 ? SocialLife.T("reallife_social_people_hints").Replace("{list}", text.ToString()) : SocialLife.T("reallife_social_people_all_met") }
            };
            var entry = new DialogEntry { messageData = "reallife_social_people_overview".Localize(data), OnCancel = PhoneContacts.HangUp };
            if (_known.Count > 0)
            {
                entry.ConfirmTextOverride = "reallife_social_btn_next".Localize();
                entry.OnConfirm = () => Page(0);
            }
            return entry;
        }

        private DialogEntry Page(int at)
        {
            SocialPerson p = _known[at % _known.Count];
            Dictionary<string, string> data = SocialLife.Data(p);
            data["age"] = SocialTalk.AgeOf(p).ToString();
            data["stage"] = SocialLife.StageName(p);
            data["mood"] = SocialLife.MoodName(p);
            data["closeness"] = p.Closeness.ToString();
            data["f"] = Mathf.RoundToInt(p.Familiarity).ToString();
            data["a"] = Mathf.RoundToInt(p.Affection).ToString();
            data["t"] = Mathf.RoundToInt(p.Trust).ToString();
            data["said"] = string.IsNullOrEmpty(p.LastSaid) ? SocialLife.T("reallife_social_people_nothing_said") : p.LastSaid;
            data["wants"] = SocialLife.T(SocialCatalogue.Outing(p.Wants).Name);
            int birthday = SocialTalk.BirthdayIn(p);
            data["birthday"] = birthday == 0 ? SocialLife.T("reallife_social_people_birthday_today")
                : SocialLife.T("reallife_social_people_birthday_in").Replace("{days}", birthday.ToString());
            var entry = new DialogEntry { messageData = "reallife_social_people_page".Localize(data), OnCancel = PhoneContacts.HangUp };
            entry.ConfirmTextOverride = "reallife_social_btn_next".Localize();
            entry.OnConfirm = () => Page(at + 1);
            entry.SecondOptionTextOverride = "reallife_social_btn_history";
            entry.OnSecondOption = () => History(at);
            return entry;
        }

        private DialogEntry History(int at)
        {
            SocialPerson p = _known[at % _known.Count];
            int day = SocialLife.Day;
            var text = new StringBuilder(256);
            for (int i = p.Memories.Count - 1; i >= 0; i--)
            {
                int ago = Mathf.Max(0, day - p.Memories[i].Day);
                text.Append(text.Length > 0 ? "\n" : string.Empty).Append(SocialLife.T(SocialCatalogue.MemoryName(p.Memories[i].Code))).Append(" (")
                    .Append(ago == 0 ? SocialLife.T("reallife_social_people_today") : SocialLife.T("reallife_social_people_days_ago").Replace("{days}", ago.ToString()))
                    .Append(')');
            }
            var traits = new StringBuilder(64);
            for (int i = 0; i < SocialCatalogue.TraitNames.Length; i++)
            {
                if (((int)p.Traits & (1 << i)) != 0)
                {
                    traits.Append(traits.Length > 0 ? ", " : string.Empty).Append(SocialLife.T(SocialCatalogue.TraitNames[i]));
                }
            }
            Dictionary<string, string> data = SocialLife.Data(p);
            data["memories"] = text.Length > 0 ? text.ToString() : SocialLife.T("reallife_social_people_no_memories");
            data["traits"] = traits.ToString();
            data["days"] = Mathf.Max(0, day - p.MetDay).ToString();
            data["money"] = p.Debt > 0 ? SocialLife.T("reallife_social_people_you_owe").Replace("{amount}", SocialLife.Money(p.Debt))
                : p.Loan > 0 ? SocialLife.T("reallife_social_people_owes_you").Replace("{amount}", SocialLife.Money(p.Loan))
                : string.Empty;
            var entry = new DialogEntry { messageData = "reallife_social_people_history".Localize(data), OnCancel = PhoneContacts.HangUp };
            entry.ConfirmTextOverride = "reallife_social_btn_next".Localize();
            entry.OnConfirm = () => Page(at + 1);
            return entry;
        }
    }
}
