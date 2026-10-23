using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The scene's arrests, and the player stopping a runner and holding them for the police.</summary>
    internal abstract partial class CrimeScene
    {
        private const float CatchReach = 1.7f;          // metres: the player has hold of a runner this close
        private const float LetGoDistance = 7f;         // metres: and has let them go this far away
        private const float BreakAwayChance = 0.4f;     // of people the player holds: one try each

        private const int StepHold = 104;
        private const int StepBreak = 105;

        private ActorRole _arrested;
        private ActorRole _held;
        private readonly ArrestSequence _arrest = new ArrestSequence();
        private bool _arrestTold;
        private bool _citizen;
        private bool _brokeAway;
        private bool _fastRunner;
        private float _breakAt;
        private float _caughtSince;
        private string _arrestEnding;

        /// <summary>Somebody running whom the player could stop by getting to them; null when nobody is. Set with RunFor.</summary>
        protected ActorRole Catchable;

        /// <summary>
        /// The officer has caught up with someone: from here ArrestSequence plays it out. The ending is announced as the cuffs
        /// go on; an empty name if the scene has said it already. With no officer on the street the suspect runs.
        /// </summary>
        /// <param name="byPlayer">The player stopped them: they are thanked when the cuffs go on.</param>
        protected void Arrest(ActorRole suspect, float now, string endingName = null, bool byPlayer = false)
        {
            Catchable = null;
            _held = null;
            if (!Cop.Present)
            {
                Flee(suspect, Scene);
                Go(StepLeaving, now, 20f);
                return;
            }
            _arrested = suspect;
            _arrestEnding = endingName;
            _arrestTold = false;
            _citizen |= byPlayer;
            if (Indoors)
            {
                _arrest.BeginIndoors(suspect, Cop, IndoorExit, now);
            }
            else
            {
                _arrest.Begin(suspect, Cop, now);
            }
            Go(StepArrest, now, 110f);
            _deadline = Mathf.Max(_deadline, now + 115f);   // an arrest once begun is played to the end
        }

        /// <summary>Indoors, where an arrested suspect is led out to.</summary>
        protected virtual Vector3 IndoorExit => Scene;

        private void RunShared(float now, Vector3 player)
        {
            switch (Step)
            {
                case StepArrest:
                    _arrest.Tick(now, player);
                    if (_arrest.Cuffed && !_arrestTold)
                    {
                        _arrestTold = true;
                        if (_citizen)
                        {
                            RewardCatch();
                        }
                        else if (_arrestEnding == null)
                        {
                            Conclude();
                        }
                        else if (_arrestEnding.Length > 0)
                        {
                            Conclude(_arrestEnding);
                        }
                        DismissOthers(_arrested.Position);
                    }
                    if (_arrest.Finished || now >= StepEnd)
                    {
                        _arrest.Clear();
                        Go(StepLeaving, now, 14f);
                    }
                    break;

                case StepHold:
                    RunHold(now, player);
                    break;

                case StepBreak:
                    RunBreak(now, player);
                    break;

                case StepLeaving:
                    if ((!First.Present && !Second.Present && !Third.Present && !Cop.Present) || now >= StepEnd)
                    {
                        End();
                    }
                    break;

                default:
                    End();
                    break;
            }
        }

        private void DismissOthers(Vector3 from)
        {
            Dismiss(First, from);
            Dismiss(Second, from);
            Dismiss(Third, from);
        }

        private void Dismiss(ActorRole role, Vector3 from)
        {
            if (role == _arrested || role == _held || !role.Present)
            {
                return;
            }
            role.Quiet();
            role.Drop();
            role.WalkAwayFrom(from, Pace.Walk);
        }

        /// <summary>
        /// Somebody runs in a way the player can do something about. Most only jog, looking over their shoulder, so a player
        /// on foot catches up; one in four really runs. Arm's reach for a moment stops them (HoldForPolice).
        /// </summary>
        protected void RunFor(ActorRole who, Vector3 from, float distance = 60f)
        {
            if (!who.Present)
            {
                return;
            }
            if (StreetSpots.AwayFrom(who.Position, from, distance, onRoad: false, out Vector3 far))
            {
                who.WalkOff(far, RunnerPace, 30f);
                Catchable = who;
            }
            else
            {
                who.Exit();
            }
        }

        /// <summary>How fast this scene's runner goes: rolled once per scene.</summary>
        protected Pace RunnerPace => _fastRunner ? Pace.Run : Pace.Jog;

        private bool TryCatch(float now, Vector3 player)
        {
            if (!Catchable.Present)
            {
                Catchable = null;
                return false;
            }
            if (Helpers.PlayerHelper.IsUsingVehicle || (Catchable.Position - player).sqrMagnitude > CatchReach * CatchReach)
            {
                _caughtSince = 0f;
                return false;
            }
            if (_caughtSince <= 0f)
            {
                _caughtSince = now;   // still within reach at the next look counts as caught
                return false;
            }
            HoldForPolice(Catchable, now, player);
            return true;
        }

        /// <summary>
        /// A citizen's arrest: the held person stands with their hands up while an officer comes at a run, as long as the
        /// player stays. Some try once to break away; walking off lets them go at no cost. The officer makes it an arrest.
        /// </summary>
        protected void HoldForPolice(ActorRole who, float now, Vector3 player)
        {
            Catchable = null;
            _held = who;
            _citizen = true;
            _caughtSince = 0f;
            who.Quiet();
            who.Drop();
            who.Stand(player);
            who.Surrender();
            // A victim who was giving chase catches up and stands by.
            if (Second != who && Second.Present)
            {
                Second.Quiet();
                Second.Go(Crime.Short(who.Position, Second.Position, 2.4f), Pace.Jog, who.Position, 12f);
            }
            if (!Cop.Present && !Indoors)
            {
                SendOfficer(who.Position, 14f, 24f);
            }
            _breakAt = !_brokeAway && Random.value < BreakAwayChance ? now + Random.Range(3f, 6f) : 0f;
            Crime.Show(NotificationType.Success, Pick(HoldLines), 7f);
            Go(StepHold, now, 45f);
            _deadline = Mathf.Max(_deadline, now + 60f);
        }

        private void RunHold(float now, Vector3 player)
        {
            if (!_held.Present)
            {
                End();
                return;
            }
            if (Cop.Present && Within(Cop, _held, 3f))
            {
                Arrest(_held, now, null, byPlayer: true);
                return;
            }
            if (Cop.Present)
            {
                Pursue(Cop, _held, Pace.Run, now);
            }
            if ((player - _held.Position).sqrMagnitude > LetGoDistance * LetGoDistance)
            {
                Crime.Show(NotificationType.Info, Pick(LetGoLines), 6f);
                LetGo(now);
            }
            else if (_breakAt > 0f && now >= _breakAt)
            {
                _breakAt = 0f;
                _brokeAway = true;
                _held.Quiet();
                if (StreetSpots.AwayFrom(_held.Position, player, 45f, onRoad: false, out Vector3 far))
                {
                    _held.Go(far, Pace.Jog, far, 16f);
                    Crime.Show(NotificationType.Warning, Pick(BreakLines), 6f);
                    Go(StepBreak, now, 14f);
                }
            }
            else if (now >= StepEnd)
            {
                // No officer could be had: let go with a flea in their ear.
                Crime.Show(NotificationType.Info, "reallife_crime_citizen_nobody", 7f);
                ModState.AddModifier(ModState.Helped);
                LetGo(now);
            }
        }

        private void RunBreak(float now, Vector3 player)
        {
            if (!_held.Present)
            {
                End();
                return;
            }
            if (Cop.Present)
            {
                Pursue(Cop, _held, Pace.Run, now);
                if (Within(Cop, _held, 2.5f))
                {
                    Arrest(_held, now, null, byPlayer: true);
                    return;
                }
            }
            if (!Helpers.PlayerHelper.IsUsingVehicle && (player - _held.Position).sqrMagnitude < CatchReach * CatchReach)
            {
                // Caught again: that was their one try.
                _held.Stand(player);
                _held.Surrender();
                Crime.Show(NotificationType.Success, "reallife_crime_citizen_recaught", 6f);
                Go(StepHold, now, 35f);
            }
            else if (now >= StepEnd || _held.Settled)
            {
                Crime.Show(NotificationType.Info, "reallife_crime_citizen_slipped", 6f);
                LetGo(now);
            }
        }

        private void LetGo(float now)
        {
            ActorRole who = _held;
            Flee(who, Helpers.PlayerHelper.GetPosition());
            DismissOthers(who.Position);
            if (Cop.Present)
            {
                Cop.WalkAwayFrom(who.Position, Pace.Walk);
            }
            _held = null;
            _citizen = false;
            Go(StepLeaving, now, 22f);
        }

        /// <summary>The player's catch is in handcuffs: thanks, a lift in mood, now and then a reward.</summary>
        protected virtual void RewardCatch()
        {
            ModState.AddModifier(ModState.Helped);
            if (Second.Present && Second != _arrested)
            {
                Second.Emote(CharacterEmojiName.CommonGreenHappy, 3f);
            }
            float roll = Random.value;
            if (roll < 0.35f)
            {
                int reward = CrimeRules.Thanks(Random.value, CrimeRules.RewardLeast, CrimeRules.RewardMost, Prices.Level);
                Crime.Give(reward, "reallife_transaction_reward");
                Crime.ShowAmount(NotificationType.Success, Pick(RewardLines), reward, 9f);
            }
            else if (roll < 0.5f)
            {
                ModState.AddModifier(ModState.MoodUpBig);
                Crime.Show(NotificationType.Success, Pick(CommendLines), 9f);
            }
            else
            {
                Crime.Show(NotificationType.Success, Pick(ThanksLines), 9f);
            }
        }

        private string Pick(string[] lines)
        {
            string line = CrimeDeck.DrawText(lines, _lastLine, Crime.Random01);
            _lastLine = line;
            return line;
        }

        private static readonly string[] HoldLines =
        {
            "reallife_crime_citizen_hold_1", "reallife_crime_citizen_hold_2", "reallife_crime_citizen_hold_3"
        };
        private static readonly string[] BreakLines =
        {
            "reallife_crime_citizen_break_1", "reallife_crime_citizen_break_2"
        };
        private static readonly string[] LetGoLines =
        {
            "reallife_crime_citizen_letgo_1", "reallife_crime_citizen_letgo_2"
        };
        private static readonly string[] ThanksLines =
        {
            "reallife_crime_citizen_thanks_1", "reallife_crime_citizen_thanks_2", "reallife_crime_citizen_thanks_3"
        };
        private static readonly string[] RewardLines =
        {
            "reallife_crime_citizen_reward_1", "reallife_crime_citizen_reward_2"
        };
        private static readonly string[] CommendLines =
        {
            "reallife_crime_citizen_commend_1", "reallife_crime_citizen_commend_2"
        };
    }
}
