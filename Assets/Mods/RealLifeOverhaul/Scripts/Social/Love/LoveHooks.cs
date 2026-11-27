using System;
using Entities;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The few places the social life asks the love life: may somebody be asked out while the player is taken, who is the second, who is
    /// quiet after an awkward night. Each is a field read or a short loop over the cast, asked on a press or an hour, never per frame.
    /// </summary>
    internal static class LoveHooks
    {
        /// <summary>The option: a second relationship, secret from the first.</summary>
        public static bool SeeingOn => Settings.Social && Settings.LoveSeeing;

        /// <summary>The option: an evening that can end at theirs or the player's. Not in a Going Public session, where the clock is everybody's and no night can pass.</summary>
        public static bool HookupsOn => Settings.Social && Settings.LoveHookups && !GoingPublic.ClockShared;

        /// <summary>The option: a night can bring a child. It needs Family's children too.</summary>
        public static bool SurpriseOn => HookupsOn && Settings.LoveSurprise && Settings.Family && Settings.FamilyChildren;

        public static bool Taken => SocialState.Loaded && SocialState.Partner != null;

        /// <summary>The played character is a woman, as the game's character data says; false when it does not say.</summary>
        public static bool PlayerFemale()
        {
            try
            {
                var data = Helpers.PlayerHelper.CharacterData;
                return data != null && data.gender == BigAmbitions.Characters.Gender.Female;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool HasSecond => LoveState.Loaded && LoveState.SecondPerson != null;

        /// <summary>Ask them out is on the menu: with no partner as ever, with one only for a second person while the option is on.</summary>
        public static bool CanDate => LoveRules.MayAsk(SeeingOn, Taken, HasSecond);

        /// <summary>The player has a partner and is about to ask somebody else: the phone warns first.</summary>
        public static bool NeedsWarning => LoveRules.WarnsBeforeAsking(SeeingOn, Taken, HasSecond);

        /// <summary>Asking them out is possible: not already together, a friend or more, somebody the player could fall for, and the rule above.</summary>
        public static bool MayAskOut(SocialPerson p)
        {
            return p != null && !p.Together && p.Stage >= SocialStage.Friend && CanDate && SocialLife.Romantic(p);
        }

        /// <summary>
        /// The person was asked and said yes while the player has a partner: they are the second, secret from the first,
        /// and the first from them. Called before their stage is set, so Family goes on seeing the first as the partner.
        /// </summary>
        public static void BeginSecond(SocialPerson p)
        {
            if (!LoveState.Loaded || p == null || !Taken)
            {
                return;
            }
            LoveFile f = LoveState.File;
            f.EndSecond();
            f.Second = p.Index;
            f.SecondSince = SocialLife.Day;
            SocialPerson first = SocialState.Partner;
            Notifications.Show(NotificationType.Warning, "reallife_love_second_begun",
                SocialLife.Data(p, "partner", first != null ? SocialLife.First(first) : string.Empty), 12f);
            SocialDirector.Logger?.Info("Love: " + p.Name + " said yes while the player was with " + (first != null ? first.Name : "somebody") + ": a second relationship, secret from the first.");
        }

        /// <summary>A post on Flare tagged somebody: if they are one of the two, the other may see it.</summary>
        public static void Tagged(int person)
        {
            if (!SeeingOn || !LoveState.Loaded || LoveState.File.Second < 0)
            {
                return;
            }
            SocialPerson first = LoveState.FirstPerson;
            int side = first != null ? LoveState.File.SideOf(person, first.Index) : -1;
            if (side >= 0)
            {
                LoveState.File.TagDay[side] = SocialLife.Day;
            }
        }

        /// <summary>After an awkward night they do not get in touch of their own accord for a few days.</summary>
        public static bool Quiet(SocialPerson p, int day)
        {
            if (!LoveState.Loaded || p == null)
            {
                return false;
            }
            LoveFile f = LoveState.File;
            for (int i = 0; i < f.Hooks.Count; i++)
            {
                if (f.Hooks[i].Person == p.Index && f.Hooks[i].QuietUntil > day)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A person's stage changed (a break-up): the second may now be the only one.</summary>
        public static void Changed()
        {
            if (LoveState.Loaded)
            {
                LoveState.Tidy();
            }
        }
    }
}
