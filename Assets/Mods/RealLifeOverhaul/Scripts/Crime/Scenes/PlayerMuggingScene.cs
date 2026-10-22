using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The player is mugged at night on foot. Handing it over costs a capped share of the cash; refusing is a coin toss
    /// between the mugger losing their nerve and the player hurt and robbed of a little anyway; walking away in time works
    /// too. With Permadeath on, the first hurt is a near miss and a later one may end the life.
    /// </summary>
    internal sealed class PlayerMuggingScene : CrimeScene
    {
        private const int Approach = 0, Demand = 1, Getaway = 2;
        internal const float Share = 0.15f;   // of the cash carried
        internal const int Least = 20;        // dollars at the start's prices
        internal const int Most = 400;        // dollars at the start's prices

        private int _asked;        // one more with every prompt, so an answer to an old one is ignored
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
                        Conclude("avoided");   // out-walked, or into a car
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
                        CloseStalePrompt();   // no answer, or the player ran
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
                StepEnd = Mathf.Min(StepEnd, now + 3f);   // something else has the player's attention: soon off
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

        // From the game's UI, later: the scene may be long over.
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
                    float energy = SaveGameManager.Current.Energy > 35f ? 8f : 0f;   // never enough to make a tired player faint
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
