using System;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The outing's part for the person's passion (SocialPassionRules): how the player turned up, what the facts of the day are, whether lateness
    /// counts, and the line the card gives when their passion moved it. COST: a few reads once an outing, at the hello and at the end.
    /// </summary>
    public sealed partial class OutingScene
    {
        private SocialArrival _arrival;          // how the player came, judged at the hello
        private bool _arrElectric, _arrLuxury;   // ...and the car they came in
        private bool _inUniform;                 // the player wore a work uniform at the hello

        /// <summary>Whether being late counts for nothing with them: they run late themselves, or live for the night.</summary>
        private bool LateIsFine => _who.Has(SocialTrait.Late) || !SocialPassionRules.MindsLate(_who);

        // At the hello: how they came and what they wore, before the player drives off or changes.
        private void NoteHowTheyCame()
        {
            _arrival = SocialArrivalNote.Read(SocialLife.Now, out _arrElectric, out _arrLuxury);
            var facts = new SocialFacts();
            try
            {
                SocialLinks.FillFacts?.Invoke(ref facts);
            }
            catch (Exception e)
            {
                SocialDirector.Logger?.Error(e);
            }
            _inUniform = facts.InUniform;
        }

        /// <summary>What a person who reads the player's way of turning up knows of today, at the end of an outing.</summary>
        private SocialFacts OutingFacts()
        {
            var f = new SocialFacts();
            try
            {
                SocialLinks.FillFacts?.Invoke(ref f);
            }
            catch (Exception e)
            {
                SocialDirector.Logger?.Error(e);   // the facts that could not be read stay unknown
            }
            int day = SocialLife.Day;
            f.Arrival = _arrival;
            f.CarElectric = _arrElectric;
            f.CarLuxury = _arrLuxury;
            f.InUniform = _inUniform;
            f.Helped = SocialState.HelpedDay > 0 && day - SocialState.HelpedDay <= SocialPassionRules.HelpedDays;
            int fresh = SocialPlaces.NewestBusinessDays(day);
            f.Rose = (SocialState.RiseDay > 0 && day - SocialState.RiseDay <= SocialPassionRules.RiseDays) || (fresh >= 0 && fresh <= SocialPassionRules.OpenedDays);
            f.AtSight = _kind == PlanKind.Walk && SocialLinks.NearSight != null && _venue.Valid && SocialLinks.NearSight(_venue.Front);
            f.Special = SocialPassionRules.IsSpecialDay(_who, day, SocialTalk.BirthdayIn(_who), SocialPlaces.DaysPerYear);
            return f;
        }

        // What their passion did to the outing, said on the card: by name once it is found, by a hint of what it is before.
        private void PassionCardLines(float delta, in SocialTilt tilt)
        {
            if (!SocialPassionRules.Active(_who) || tilt.Led == SocialTrait.None || Mathf.Abs(delta) < SocialPassionRules.CardLeast)
            {
                return;
            }
            int at = SocialPassionRules.IndexOf(tilt.Led);
            if (at < 0)
            {
                return;
            }
            string line = SocialPassionGame.CardLine(_who, at, delta);
            if (line != null)
            {
                _text.Append(line).Append('\n');
            }
        }
    }
}
