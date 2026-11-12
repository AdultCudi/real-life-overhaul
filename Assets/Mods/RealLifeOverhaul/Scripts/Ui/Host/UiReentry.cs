using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A call that must not come back into itself before it has ended: a screen laid out from inside its own layout (UiHost), the
    /// game's screen shown again from inside its own showing (UiGameHost), a picture asked for from inside a picture's answer
    /// (UiAnswers). Its owner enters it as the call starts and leaves it as the call ends; a call that comes back in while it is
    /// entered is refused, so no chain of such calls can run the stack out, whatever the game or another mod calls in between. The
    /// first refusal at each place in a session is said once through the mod's logger with the stack that came back in, so the next
    /// player's log names the way round. Game-free. Cost: a comparison and two writes a call; the stack is read only at a refusal, once a session at each place.
    /// </summary>
    public sealed class UiReentry
    {
        private const int MostPlaces = 8;   // places said in a session at most: each guard names one, a handful in all

        private static readonly string[] Told = new string[MostPlaces];
        private static int _told;
        private bool _in;

        /// <summary>Told the first refusal at each place in a session; set by the game file (the mod's logger).</summary>
        public static Action<string> Said;

        /// <summary>Calls refused in this session, at every place.</summary>
        public static int Refusals { get; private set; }

        /// <summary>True while the call is running.</summary>
        public bool In => _in;

        /// <summary>The call starts: true to run it, false when it has come back into itself (the caller refuses it and calls <see cref="Refused"/>).</summary>
        public bool Enter()
        {
            if (_in)
            {
                return false;
            }
            _in = true;
            return true;
        }

        /// <summary>The call has ended, by any way out.</summary>
        public void Leave()
        {
            _in = false;
        }

        /// <summary>
        /// A call refused at <paramref name="place"/> (about <paramref name="what"/>, a screen's name or a key): counted, and said once a
        /// session at that place with the stack that came back in.
        /// </summary>
        public static void Refused(string place, string what)
        {
            Refusals++;
            for (int i = 0; i < _told; i++)
            {
                if (string.Equals(Told[i], place, StringComparison.Ordinal))
                {
                    return;
                }
            }
            if (_told >= MostPlaces)
            {
                return;
            }
            Told[_told++] = place;
            Said?.Invoke("UI: " + place + (string.IsNullOrEmpty(what) ? string.Empty : " (" + what + ")")
                         + " was called again before it had ended; the second call was refused, so it cannot go round until the stack runs out. Said once a session here. The way it came back in: "
                         + Environment.StackTrace);
        }

        /// <summary>A new session for the offline checks: no place said yet, no refusal counted.</summary>
        public static void Forget()
        {
            Array.Clear(Told, 0, Told.Length);
            _told = 0;
            Refusals = 0;
        }
    }
}
