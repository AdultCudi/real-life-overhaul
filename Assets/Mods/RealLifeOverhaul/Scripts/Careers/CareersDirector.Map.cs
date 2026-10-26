using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Show on map for the working life: a vacancy, an application and the job held each lead to the employer's building as the game's own
    /// destination, with the phone closed so the marker shows; and the city map lists the buildings with a vacancy open (World/MapLinks).
    /// Cost: on a press only; the map's look every three seconds reads the open posts once.
    /// </summary>
    public sealed partial class CareersDirector
    {
        /// <summary>Show on map for a post (a vacancy or an application). Null if done, else a reason key.</summary>
        internal string ShowPost(Opening post)
        {
            return post == null ? "reallife_work_why_gone" : ShowPlace(post.Employer, post.Street, post.Number);
        }

        /// <summary>Show on map for an employer that hires at a help-wanted board (the gig work's own). Null if done, else a reason key.</summary>
        internal string ShowBoard(BoardEmployer board)
        {
            Func<int, string> show = CareersBridges.ShowBoard;
            string why = show != null ? show(board.Index) : "reallife_career_why_route";
            if (why != null)
            {
                return why;
            }
            CloseApps();
            Notifications.Show(NotificationType.Success, "reallife_career_office_shown", Data("employer", board.Name, "address", board.Address ?? string.Empty), 6f);
            return null;
        }

        /// <summary>Show on map for the job held. Null if done, else a reason key.</summary>
        internal string ShowWork()
        {
            Job j = Work != null ? Work.Job : null;
            return j == null ? "reallife_work_why_no_job" : ShowPlace(j.Post.Employer, j.Post.Street, j.Post.Number);
        }

        // The one way: the game's destination on the building, the phone closed, and a notice saying where.
        private string ShowPlace(string employer, string street, int number)
        {
            if (string.IsNullOrEmpty(street) || !DeskGame.Route(street, number))
            {
                return "reallife_career_why_route";
            }
            CloseApps();
            string address = WorkGame.Address(street, number);
            Notifications.Show(NotificationType.Success, "reallife_career_post_shown",
                Data("employer", CareersText.Employer(employer), "address", address), 6f);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Careers check (show on map): '" + CareersText.Employer(employer) + "' at " + address + " is the game's destination: PASS.");
            }
            return null;
        }

        /// <summary>
        /// The posts open today, for the city map's row of buildings that are hiring: one list, refilled. A read of the open posts, never
        /// of the city; false when the working life is off.
        /// </summary>
        internal bool Hiring(List<Opening> into)
        {
            into.Clear();
            WorkFile w = Work;
            if (w == null || Save == null)
            {
                return false;
            }
            int day = Save.Day;
            for (int i = 0; i < w.Openings.Count; i++)
            {
                Opening o = w.Openings[i];
                if (o != null && o.Open(day) && !string.IsNullOrEmpty(o.Street))
                {
                    into.Add(o);
                }
            }
            return true;
        }

        /// <summary>Changes whenever the posts open today do, cheaply (no allocation): the day and each open post's id.</summary>
        internal long HiringStamp()
        {
            WorkFile w = Work;
            if (w == null || Save == null)
            {
                return 0L;
            }
            int day = Save.Day;
            long stamp = 1L;
            unchecked
            {
                for (int i = 0; i < w.Openings.Count; i++)
                {
                    Opening o = w.Openings[i];
                    if (o != null && o.Open(day) && !string.IsNullOrEmpty(o.Street))
                    {
                        stamp = stamp * 31 + o.Id + 1;
                    }
                }
            }
            return stamp == 0L ? 1L : stamp;
        }
    }
}
