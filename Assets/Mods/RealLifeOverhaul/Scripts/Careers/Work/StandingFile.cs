using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>One worked shift, kept for the Career app's recent shifts: the day, the minutes worked, the score that day and the pay in whole money.</summary>
    public struct ShiftRecord
    {
        public int Day, Minutes, Score, Pay;
    }

    /// <summary>What a person beside the player is: the saved number of <see cref="Peer.Role"/>.</summary>
    public enum PeerRole
    {
        Colleague = 0,   // on the shift
        Lead = 1,        // the player's lead
        UpForPost = 2,   // a colleague up for the same next rung
        Applicant = 3    // another applicant for a post the player has seen
    }

    /// <summary>
    /// A named person who stands beside the player at work, or applies for the same post. One record serves both, so the rules that judge
    /// the player judge them the same way. Game-free.
    /// </summary>
    public sealed class Peer
    {
        public const int RingSize = 12;

        public string Name = string.Empty;   // a person of the city's people pool (PersonId), kept so the same person stays
        public int PersonId;                  // their id in the pool; 0 for a name of the catalogue (the population off, or an older save)
        public int Rank;                      // the rank they hold (a colleague) or apply for (an applicant)
        public int Skill;                     // 0 to 100: where their record settles
        public int Day;                       // the day they joined the shift, or applied
        public int Post;                      // an applicant's vacancy (Opening.Id); 0 for a colleague
        public int Role;                      // PeerRole
        public int Score;                     // the score now, 0 to 100
        public int Punctuality, Reliability, Teamwork;   // the same standing the player has, 0 to 100 each
        public int Count;                     // weeks in the ring
        public readonly int[] Ring = new int[RingSize];   // the weekly score, oldest first

        /// <summary>Adds a week's score; the oldest goes when the ring is full.</summary>
        public void Push(int score)
        {
            StandingFile.PushTo(Ring, ref Count, score);
        }

        /// <summary>The mean of the weeks held, whole points; the score now while none.</summary>
        public int Mean() => Count == 0 ? Score : StandingFile.MeanOf(Ring, Count);
    }

    /// <summary>
    /// The player's standing at work and the people beside them, tied to the job as the ladder is (its first day and place), in its own
    /// line of the save (<see cref="StandingStore"/>). Game-free.
    /// </summary>
    public sealed class StandingFile
    {
        public int Hired;                                          // the job it belongs to: its first day...
        public string Street = string.Empty, Employer = string.Empty;   // ...where...
        public int Number;
        public int Seed = 1;                                       // for the weekly drift
        public int WeekOf = -1;                                    // the last week (day / 7) the ring was written
        public int Count;                                          // the player's weeks in the ring
        public readonly int[] Ring = new int[Peer.RingSize];       // the player's weekly performance
        public readonly List<Peer> Peers = new List<Peer>(StandingRules.PeersMost);    // colleagues of the job
        public readonly List<Peer> Others = new List<Peer>(StandingRules.OthersMost);  // other applicants, by post
        public readonly List<ShiftRecord> Shifts = new List<ShiftRecord>(StandingRules.ShiftsMost);   // the last shifts worked, oldest first

        public bool Of(Job j) => j != null && j.Hired == Hired && j.Post.Street == Street && j.Post.Number == Number && j.Post.Employer == Employer;

        public void Push(int score)
        {
            PushTo(Ring, ref Count, score);
        }

        public int Mean() => Count == 0 ? 0 : MeanOf(Ring, Count);

        /// <summary>The colleague up for the same rung, or null.</summary>
        public Peer UpFor()
        {
            for (int i = 0; i < Peers.Count; i++)
            {
                if (Peers[i].Role == (int)PeerRole.UpForPost)
                {
                    return Peers[i];
                }
            }
            return null;
        }

        public static void PushTo(int[] ring, ref int count, int score)
        {
            if (count < ring.Length)
            {
                ring[count++] = score;
                return;
            }
            for (int i = 1; i < ring.Length; i++)
            {
                ring[i - 1] = ring[i];
            }
            ring[ring.Length - 1] = score;
        }

        public static int MeanOf(int[] ring, int count)
        {
            int sum = 0;
            for (int i = 0; i < count; i++)
            {
                sum += ring[i];
            }
            return count == 0 ? 0 : (sum + count / 2) / count;
        }
    }
}
