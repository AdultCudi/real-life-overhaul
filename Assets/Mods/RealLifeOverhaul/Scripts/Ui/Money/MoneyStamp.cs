using System;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a cash figure or a loan on the money apps' pages would change with: the cash and the loans' balances, looked at four times a second (never every frame), so a page that
    /// shows them is written again when another system spends or pays while the app is open, and not only at the next hour.
    /// </summary>
    internal static class MoneyStamp
    {
        private const float Every = 0.25f;   // seconds between looks
        private static float _next;
        private static int _stamp;

        public static int Now
        {
            get
            {
                float now = Time.unscaledTime;
                if (now >= _next)
                {
                    _next = now + Every;
                    _stamp = Look();
                }
                return _stamp;
            }
        }

        private static int Look()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return 0;
            }
            unchecked
            {
                int sum = (int)Math.Round(save.Money);
                sum = sum * 31 + (save.Loans != null ? save.Loans.Count : 0);
                for (int i = 0; save.Loans != null && i < save.Loans.Count; i++)
                {
                    sum = sum * 31 + (int)save.Loans[i].remainingAmount;
                }
                return sum;
            }
        }
    }
}
