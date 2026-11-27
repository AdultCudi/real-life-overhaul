using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The doors other features knock on: safe at any time, a null check while the social life is not running.</summary>
    public static class SocialHooks
    {
        /// <summary>The player helped somebody: a 911 call that brought help, a thief stopped, a wallet returned.</summary>
        public static void Helped()
        {
            SocialDirector director = SocialDirector.Instance;
            if (director != null)
            {
                director.OnHelped();
            }
        }

        /// <summary>The player's career rose a rung (a promotion on the job, or on the ladder): a career focused person is glad of it for a while.</summary>
        public static void CareerRose(int day)
        {
            if (SocialState.Loaded && day > 0)
            {
                SocialState.RiseDay = day;
            }
        }

        /// <summary>The player got off a bus or out of the subway: the people they meet in the next while read that they came by the city's transport.</summary>
        public static void RodeTransit()
        {
            SocialArrivalNote.Transit(SocialLife.Now);
        }

        /// <summary>At a coffee cart.</summary>
        public static void BoughtCoffee()
        {
            SocialDirector director = SocialDirector.Instance;
            if (director != null)
            {
                director.OnCoffee();
            }
        }

        public static void PlayerArrested()
        {
            SocialDirector director = SocialDirector.Instance;
            if (director != null)
            {
                director.OnArrested();
            }
        }

        /// <summary>A selfie saved: the friend beside the player on an outing likes it, once an outing. Their first name, or null.</summary>
        public static string Selfie(Vector3 where)
        {
            SocialDirector director = SocialDirector.Instance;
            return director != null ? director.OnSelfie(where) : null;
        }

        /// <summary>The index of the friend on an outing beside <paramref name="where"/> now, or -1.</summary>
        public static int Companion(Vector3 where)
        {
            SocialDirector director = SocialDirector.Instance;
            return director != null ? director.Companion(where) : -1;
        }
    }
}
