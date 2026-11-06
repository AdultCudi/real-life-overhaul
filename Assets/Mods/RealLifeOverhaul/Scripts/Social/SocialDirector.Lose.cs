using System;
using System.Collections.Generic;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Losing touch. The player lets somebody go from their page in People, after the game's own question box; once every slot
    /// of the cast is taken, an acquaintance who drifted away long ago goes by themselves to make room. Whoever goes leaves the
    /// phone, People, Flare and RLO Texts' memory, and somebody new takes their slot, to be met where they would have been.
    /// COST: nothing a frame; a pass over the cast and the save's contacts once, when somebody goes.
    /// </summary>
    public sealed partial class SocialDirector
    {
        private static readonly Func<SocialPerson, bool> Held = p => LoseBlockOf(p) != LoseBlock.None;
        private static readonly Func<SocialPerson, LoseBlock> BlockOf = LoseBlockOf;

        /// <summary>Why the player cannot lose touch with somebody now (<see cref="SocialRules.LoseBlockOf"/>); None: they can.</summary>
        public static LoseBlock LoseBlockOf(SocialPerson person)
        {
            if (person == null)
            {
                return LoseBlock.NotKnown;
            }
            int index = person.Index;
            bool plan = (SocialState.Plan.Active && SocialState.Plan.Person == index) || (Instance != null && Instance.WithNow(person));
            bool home = SocialLinks.LivesWith != null && SocialLinks.LivesWith(index);
            bool flare = SocialLinks.OnFlare != null && SocialLinks.OnFlare(index);
            return SocialRules.LoseBlockOf(person, plan, home, flare);
        }

        // Out on the street with the player now, or on their way to them.
        private bool WithNow(SocialPerson person)
        {
            SocialPerson with = (_scene as OutingScene)?.Who ?? (_scene as MeetScene)?.Who;
            return with == person || _pendingDropBy == person || _introFriend == person;
        }

        /// <summary>
        /// The game's question box, for somebody the player can lose touch with: a yes lets them go at once. Asked from their
        /// page in People once the call has ended, as the game's Contacts app asks before it deletes a contact.
        /// </summary>
        public static void AskLoseTouch(SocialPerson person)
        {
            if (person == null || !person.Met)
            {
                return;
            }
            Dictionary<string, string> data = SocialLife.Data(person);
            GameConfirm.Show("reallife_social_lose_ask_title".Localize(data), "reallife_social_lose_ask".Localize(data), () => LoseTouchAnswered(person), null,
                "reallife_social_lose_btn_yes", "reallife_social_lose_btn_keep", false, true);
        }

        private static void LoseTouchAnswered(SocialPerson person)
        {
            try
            {
                if (!SocialState.Loaded || SocialState.Get(person.Index) != person)
                {
                    return;   // a save loaded meanwhile, or they are gone already
                }
                LoseBlock block = LoseBlockOf(person);
                if (block != LoseBlock.None)
                {
                    string key = SocialCatalogue.LoseBlocks[(int)block];
                    if (key != null)
                    {
                        SocialLife.Toast(NotificationType.Warning, key, SocialLife.Data(person), 9f);
                    }
                    return;   // something came up between the question and the answer
                }
                Dictionary<string, string> data = SocialLife.Data(person);
                if (LoseTouch(person, person.Origin, "the player chose to") != null)
                {
                    SocialLife.Toast(NotificationType.Info, "reallife_social_lost", data, 10f);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// Somebody let go: their contact and thread off the phone, Flare's and RLO Texts' hold on them dropped, and somebody new
        /// rolled into their slot from the one who leaves (<see cref="SocialRules.ReplaceSeed"/>), to be met at
        /// <paramref name="origin"/>. Returns the newcomer; null, and nothing changed, when the slot cannot be given again.
        /// The caller has asked <see cref="LoseBlockOf"/>.
        /// </summary>
        internal static SocialPerson LoseTouch(SocialPerson person, SocialOrigin origin, string why)
        {
            if (person == null || !SocialState.Loaded || SocialState.Get(person.Index) != person)
            {
                return null;
            }
            int index = person.Index;
            SocialPerson next = SocialCatalogue.MakeRolled(index, SocialRules.ReplaceSeed(SocialState.CastSeed, index, person.Seed), origin, -1, SocialLife.Day,
                SocialPlaces.DaysPerYear, SocialPlaces.Districts, SocialState.People, SocialState.Count);
            int contacts = SocialPhone.Remove(person);
            Forget(index);
            Instance?.LetGo(person);
            if (!SocialState.Replace(next))
            {
                return null;
            }
            SocialPhone.Picture(next);
            Logger?.Info("Social: lost touch with " + person.Name + " (" + person.Stage + ", " + why + "); " + contacts + " contact(s) taken off the phone; "
                         + next.Name + " takes their place, person " + (index + 1) + ", to be met " + origin + ".");
            return next;
        }

        // Each part of the mod that keeps somebody by their slot lets them go before somebody new takes it.
        private static void Forget(int index)
        {
            try
            {
                SocialLinks.FlareForget?.Invoke(index);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            try
            {
                SocialLinks.TextsForget?.Invoke(index);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            try
            {
                SocialLinks.NetworkForget?.Invoke(index);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Whatever the director still holds of them: nobody drops by, rings or texts thanks as them.
        private void LetGo(SocialPerson person)
        {
            if (_pendingDropBy == person)
            {
                _pendingDropBy = null;
            }
            if (_callWhenFree == person)
            {
                _callWhenFree = null;
            }
            if (_thanks == person)
            {
                _thanks = null;
            }
            if (_introFriend == person)
            {
                _introFriend = null;
                _introStranger = null;
            }
        }

        /// <summary>
        /// Once every slot of the cast is taken: the acquaintance longest without a word who drifted away goes by themselves, and
        /// whoever takes their slot is the one to meet at <paramref name="origin"/>. Null when nobody may go, or relationships
        /// are kept as they are (the option "Relationships fade" off).
        /// </summary>
        private static SocialPerson MakeRoom(SocialOrigin origin)
        {
            if (!Settings.SocialRelationships)
            {
                return null;
            }
            int gone = SocialRules.LongestDrifted(SocialState.People, SocialState.Count, SocialLife.Day, Held);
            SocialPerson person = SocialState.Get(gone);
            if (person == null)
            {
                return null;
            }
            string name = person.Name;
            SocialPerson next = LoseTouch(person, origin, "drifted away " + (SocialLife.Day - person.LastContactDay) + " days ago, to make room");
            if (next != null)
            {
                SocialLife.Toast(NotificationType.Info, "reallife_social_lost_drift", new Dictionary<string, string>(1) { { "name", name } }, 9f);
            }
            return next;
        }

        /// <summary>Whether somebody could go by themselves to make room, without letting them go. One pass over the cast.</summary>
        internal static bool RoomToMake()
        {
            return Settings.SocialRelationships && SocialRules.LongestDrifted(SocialState.People, SocialState.Count, SocialLife.Day, Held) >= 0;
        }

        /// <summary>
        /// Lets go of the newest acquaintance the player could lose touch with, without asking, and checks that their slot is
        /// free: somebody new and unmet in it, their contact gone, and somebody left to meet. Says why when nobody can go.
        /// </summary>
        public void TestLoseTouch()
        {
            const string test = "Lose touch";
            if (!TestReady(test, false))
            {
                return;
            }
            SocialPerson person = SocialState.Get(SocialRules.NewestLoseable(SocialState.People, SocialState.Count, BlockOf));
            if (person == null)
            {
                TestRefusals.TellKey(Logger, test, "reallife_social_test_lose_none", "no acquaintance can be let go: family, the old friend, a partner, plans or money hold the rest");
                return;
            }
            int index = person.Index;
            string name = person.Name;
            SocialPerson next = LoseTouch(person, person.Origin, "the test button");
            SocialPerson now = SocialState.Get(index);
            bool free = next != null && now == next && !now.Met && now.Name != name && SocialPhone.ContactGone(person) && AnyStranger();
            Logger?.Info("Social check (lose touch): " + name + " in slot " + (index + 1) + " let go; the slot holds " + (now != null ? now.Name : "nobody")
                         + (now != null && !now.Met ? ", not met yet" : ", met") + ", the old contact " + (SocialPhone.ContactGone(person) ? "gone" : "still there")
                         + ", somebody to meet " + (AnyStranger() ? "yes" : "no") + ": " + (free ? "PASS" : "FAIL"));
            var data = new Dictionary<string, string>(2) { { "name", name }, { "result", SocialLife.T(free ? "reallife_social_test_lose_pass" : "reallife_social_test_lose_fail") } };
            SocialLife.Toast(free ? NotificationType.Success : NotificationType.Error, "reallife_social_test_lose_done", data, 10f);
        }
    }
}
