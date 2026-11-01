using System;

namespace RealLifeOverhaul.Sessions
{
    /// <summary>
    /// What the desk sessions ask of the rest of the mod, filled in by Wiring/Wiring.SessionLinks. Each is optional with a default;
    /// a fault in the other feature reads as the default.
    /// </summary>
    public static class SessionBridges
    {
        /// <summary>A business's manager who can take a session (the Company app's managers): their name and skill, 0 to 100; false with none at work there.</summary>
        public delegate bool ManagerAsk(string place, out string name, out int skill);

        /// <summary>The business's manager. Default: nobody, so every session waits for the player.</summary>
        public static ManagerAsk Manager;

        /// <summary>Opens the Company app on its managers' page; false if it cannot be. Default: none.</summary>
        public static Func<bool> OpenManagers;

        /// <summary><see cref="Manager"/> asked: nobody with no answer, or if the company faults.</summary>
        public static bool ManagerAt(string place, out string name, out int skill)
        {
            name = string.Empty;
            skill = 0;
            try
            {
                if (Manager != null && Manager(place, out name, out skill))
                {
                    name = name ?? string.Empty;
                    skill = Math.Max(0, Math.Min(100, skill));
                    return true;
                }
            }
            catch (Exception)
            {
                // the company's fault: nobody takes the session, and it waits for the player
            }
            name = string.Empty;
            skill = 0;
            return false;
        }

        /// <summary>
        /// True while the owner can work at this desk, the sessions' own (the tech firms' work at a desk of one's own firm, which
        /// leaves its click to the sessions there). Default: never.
        /// </summary>
        public static Func<EmployeeStationController, bool> WorkHere;

        /// <summary>The work at this desk offered, as a click on a free desk of the firm offers it. Default: nothing.</summary>
        public static Action<EmployeeStationController> Work;

        /// <summary><see cref="WorkHere"/> asked: false with no answer, or if the tech firms fault.</summary>
        public static bool WorkHereNow(EmployeeStationController desk)
        {
            try
            {
                return WorkHere != null && desk != null && WorkHere(desk);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary><see cref="Work"/> told; a fault in the tech firms changes nothing here.</summary>
        public static void WorkNow(EmployeeStationController desk)
        {
            try
            {
                Work?.Invoke(desk);
            }
            catch (Exception)
            {
                // the tech firms' fault: the desk stays as it was, and the player can click it again
            }
        }

        /// <summary><see cref="OpenManagers"/> asked: false with no answer, or if the company faults.</summary>
        public static bool OpenManagersNow()
        {
            try
            {
                return OpenManagers != null && OpenManagers();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
