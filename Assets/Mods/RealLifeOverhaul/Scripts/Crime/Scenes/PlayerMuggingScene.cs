using System;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The player is mugged: at night, on foot, somebody steps out and wants their money. They
    /// choose. Handing it over costs a capped share of the cash they carry and nothing else.
    /// Refusing is a coin toss between the mugger losing their nerve and the player being hurt
    /// (the same injury life events give) and losing a little anyway. Walking briskly away
    /// before the mugger reaches them works too. With Permadeath on the demand says what refusing
    /// can cost, the first time it ends hurt is a near miss, and after that a hurt may end the life.
    /// </summary>
    internal sealed class PlayerMuggingScene : CrimeScene
    {
        private const int Approach = 0, Demand = 1, Getaway = 2;
        internal const float Share = 0.15f;
        internal const int Least = 20;
        internal const int Most = 400;

        private int _asked;        // goes up with every prompt, so an answer to an old one is ignored
        private bool _answered;
        private bool _complied;
        private bool _promptOpen;

        public override string Id => "player_mugging";
        public override bool ShowsMarker => false;
        protected override float MaxTime => 75f;

        protected override bool Stage(in CrimeSetup setup, float now)
        {
            if (PlayerHelper.IsUsingVehicle || HudConfirm.isOpen || Crime.Cash < Prices.At(Least) * 2
                || !StreetSpots.PavementAround(setup.Player, 10f, 18f, true, out Vector3 from)
                || !First.Enter(from, ActorLook.Civilian, 3f, onRoad: true))
            {
                return false;
            }
            _answered = false;
            First.Go(setup.Player, Pace.Jog, setup.Player, 10f);
            Go(Approach, now, 16f);
            return true;
        }

        public override bool Report(float now)
        {
            return false;
        }

        protected override void Run(float now, Vector3 player)
        {
            if (!First.Present)
            {
                End();
                return;
            }
            float gapSqr = (First.Position - player).sqrMagnitude;
            switch (Step)
            {
                case Approach:
                    if (PlayerHelper.IsUsingVehicle || now >= StepEnd || gapSqr > 45f * 45f)
                    {
                        // Out-walked, or into a car: the mugger thinks better of it.
                        Conclude("avoided");
                        Flee(First, player, 40f);
                        Go(StepLeaving, now, 15f);
                    }
                    else if (gapSqr < 2.4f * 2.4f)
                    {
                        Ask(now, player);
                    }
                    else if (now >= NextRepath)
                    {
                        NextRepath = now + RepathInterval;
                        First.Go(player, Pace.Jog, player, 10f);
                    }
                    break;

                case Demand:
                    if (_answered)
                    {
                        Resolve(now, player);
                    }
                    else if (now >= StepEnd || gapSqr > 12f * 12f)
                    {
                        // No answer (the prompt was dismissed some other way), or the player simply ran.
                        CloseStalePrompt();
                        Conclude("avoided");
                        Flee(First, player, 40f);
                        Go(StepLeaving, now, 15f);
                    }
                    break;

                case Getaway:
                    if (now >= StepEnd)
                    {
                        Go(StepLeaving, now, 15f);
                    }
                    break;
            }
        }

        private void Ask(float now, Vector3 player)
        {
            if (HudConfirm.isOpen || !LifeEvents.IsSafeForEvent())
            {
                StepEnd = Mathf.Min(StepEnd, now + 3f);   // something else has the player's attention: a few more seconds, then off
                return;
            }
            First.Stand(player);
            First.Repeat(Reach, 2.5f, 4f, CharacterEmojiName.CommonRedAngry, 0.6f);
            int asked = ++_asked;
            _answered = false;
            GameConfirm.Show("reallife_crime_mugged_header", Permadeath.Mortality.MuggingBody("reallife_crime_mugged_body"),
                () => Answer(asked, true), () => Answer(asked, false),
                "reallife_crime_mugged_comply", "reallife_crime_mugged_resist", allowConfirmationSkip: false);
            _promptOpen = true;
            Go(Demand, now, 40f);
        }

        // Arrives later, from the game's UI: the scene may be long over.
        private void Answer(int asked, bool comply)
        {
            if (Active && asked == _asked && Step == Demand)
            {
                _answered = true;
                _promptOpen = false;
                _complied = comply;
            }
        }

        // Any answer still to come is ignored, and a prompt of ours still on screen is closed: left up, it would hold
        // every other event off until the player clicked it.
        private void CloseStalePrompt()
        {
            _asked++;
            if (_promptOpen && HudConfirm.isOpen)
            {
                HudConfirm.onClose?.Invoke();
            }
            _promptOpen = false;
        }

        private void Resolve(float now, Vector3 player)
        {
            First.Quiet();
            if (_complied)
            {
                int taken = Crime.Rob(Share, Least, Most, "reallife_transaction_mugging");
                ModState.AddModifier(ModState.Robbed);
                ConcludeAmount("complied", taken, NotificationType.Error);
            }
            else if (Random.value < 0.5f)
            {
                ModState.AddModifier(ModState.MoodUp);
                Conclude("scared_off");
            }
            else
            {
                // A shove, a fall, and a hand in the jacket on the way down.
                Permadeath.Verdict fate = Permadeath.Mortality.Judge(Permadeath.DeathCause.Mugging);
                int taken = Crime.Rob(Share * 0.5f, Least, Most / 2, "reallife_transaction_mugging");
                First.Gesture(Punch);
                if (fate == Permadeath.Verdict.Death && Permadeath.Mortality.Die(Permadeath.DeathCause.Mugging))
                {
                    Flee(First, player);
                    Go(Getaway, now, 2f);
                    return;
                }
                LifeEvents life = LifeEvents.Instance;
                CrimeEnding hurt = Entry.Ending("hurt");
                if (life != null && hurt != null && hurt.Texts.Length > 0)
                {
                    // Never enough to make a tired player faint.
                    float energy = SaveGameManager.Current.Energy > 35f ? 8f : 0f;
                    life.Injure(12, energy, hurt.Texts[Random.Range(0, hurt.Texts.Length)]);
                }
                if (fate == Permadeath.Verdict.NearMiss)
                {
                    Permadeath.Mortality.NearMiss(Permadeath.DeathCause.Mugging);
                }
                if (taken > 0)
                {
                    Crime.ShowAmount(NotificationType.Error, "reallife_crime_mugged_taken", taken);
                }
            }
            Flee(First, player);
            Go(Getaway, now, 2f);
        }

        protected override void Release()
        {
            CloseStalePrompt();
        }
    }
}
