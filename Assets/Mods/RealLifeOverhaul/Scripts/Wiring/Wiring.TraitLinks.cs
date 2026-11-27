using System;
using RealLifeOverhaul.Giving;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The social life's passions (Social/SocialPassionRules) and moments (SocialMomentRules) with the rest of the mod:
        ///
        ///   the two options       Hidden passions, Moments on outings: the rules ask them through their switches
        ///   FillFacts             what a person who cares about the way the player turns up reads: Home's fitness and diet, the books'
        ///                         net worth, Giving's gifts this month, and whether a work uniform is on (Jobs/JobUniform)
        ///
        /// Asked once an outing, never per frame; a fault is said once (<see cref="Fault"/>) and the facts that could not be read stay unknown.
        /// </summary>
        public static class TraitLinks
        {
            internal static void Connect()
            {
                SocialPassionRules.OnSwitch = () => Settings.Social && Settings.SocialPassions;
                SocialMomentRules.OnSwitch = () => Settings.Social && Settings.SocialOutings && Settings.SocialMoments;
                SocialLinks.FillFacts = Fill;
            }

            internal static void Disconnect()
            {
                SocialPassionRules.OnSwitch = () => true;
                SocialMomentRules.OnSwitch = () => true;
                SocialLinks.FillFacts = null;
            }

            private static void Fill(ref SocialFacts facts)
            {
                try
                {
                    if (HomeState.Loaded && Settings.HomeFitness)
                    {
                        facts.FitnessKnown = true;
                        facts.Fitness = (int)Math.Round(HomeState.Fitness);
                        float diet = HomeState.DietScore;
                        facts.Diet = diet >= HomeBalance.DietGood ? 1 : diet <= HomeBalance.DietPoor ? -1 : 0;
                    }
                }
                catch (Exception e)
                {
                    Fault("TraitLinks.Fitness", e);
                }
                try
                {
                    facts.WealthTier = SocialPassionRules.TierOf(Economy.Books.NetWorth());
                }
                catch (Exception e)
                {
                    Fault("TraitLinks.NetWorth", e);
                }
                try
                {
                    GivingState giving = GivingDirector.Instance != null && GivingDirector.Instance.Running ? GivingDirector.Instance.State : null;
                    facts.CharityMonth = giving != null && giving.MonthGiven > 0;
                }
                catch (Exception e)
                {
                    Fault("TraitLinks.Charity", e);
                }
                try
                {
                    facts.InUniform = JobUniform.AnyOn;
                }
                catch (Exception e)
                {
                    Fault("TraitLinks.Uniform", e);
                }
            }
        }
    }
}
