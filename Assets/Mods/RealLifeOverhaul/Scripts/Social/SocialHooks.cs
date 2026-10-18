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
