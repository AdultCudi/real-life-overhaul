using System;
using Entities;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the construction programme asks of the builders who run sites of their own (Scripts/Construction: the player's
    /// company and the rival builders), and of the copy engine for a finished building's new look (Scripts/NewBuildings):
    /// delegates with defaults that leave every site the city's and every building as it stands, set by
    /// Wiring/Wiring.ConstructionLinks. Each is asked on an hour, a handover or a page, never per frame; a fault there is logged
    /// and answered as if unset.
    /// </summary>
    public static class SiteBuilders
    {
        /// <summary>The hours a site works in one of its working hours, given what the city's pace makes it. Default: as given.</summary>
        public static Func<SiteState, float, float> Hours;

        /// <summary>True for a site a builder started (a plot, a rebuild of its own): the programme does not count it among its own. Default: none.</summary>
        public static Func<SiteState, bool> Owns;

        /// <summary>False while a builder's site is put away (its feature switched off): it is not set out in the street. Default: shown.</summary>
        public static Func<SiteState, bool> Shown;

        /// <summary>The name a builder's site gives on its notice board; null for the city's contractor. Default: none.</summary>
        public static Func<SiteState, string> Name;

        /// <summary>A site has been handed over: told for every site, so a builder hears of its own and of the city's it runs. Default: nothing.</summary>
        public static Action<SiteState> Handed;

        /// <summary>The programme started one of the city's own sites (it may go to tender). Default: nothing.</summary>
        public static Action<SiteState> Started;

        /// <summary>The design of a site's finished building, or null for one picked to fit it. Default: picked.</summary>
        public static Func<SiteState, string> Design;

        /// <summary>A plot's finished building's tag, its lot's id in the district, or null. Default: none.</summary>
        public static Func<SiteState, string> Tag;

        /// <summary>True when a finished building's lot holds a building of the district now, so no copy is needed there. Default: no.</summary>
        public static Func<string, bool> Taken;

        /// <summary>True when a builder's rebuild keeps its building's own look (an extension: more floors, the same front), so no copy stands over it. Default: no.</summary>
        public static Func<SiteState, bool> KeepsLook;

        /// <summary>A design stood over a building out of view, its own look hidden (BuildingCopies.PlaceOver): a handle, or null. Default: none.</summary>
        public static Func<CityBuildingController, string, object> CopyOver;

        /// <summary>A design stood on a plot's ground out of view (BuildingCopies.PlaceOnPlot): middle, yaw, width, depth; a handle, or null. Default: none.</summary>
        public static Func<string, Vector3, int, int, int, object> CopyOnPlot;

        /// <summary>A copy taken away out of view, a look it hid shown again. Default: nothing.</summary>
        public static Action<object> CopyRemove;

        /// <summary>True for a copy that is gone or was never made. Default: gone.</summary>
        public static Func<object, bool> CopyGone;

        /// <summary>City Hall's planning and building control topic, given the way back to the first topic; its first page. Default: none (five topics).</summary>
        public static Func<Func<DialogEntry>, DialogEntry> PlanningTopic;

        /// <summary>A builder's own page at its site's notice board (by the site's id), or null for the city's notice. Default: the city's.</summary>
        public static Func<int, DialogEntry> Notice;

        public static void Reset()
        {
            Hours = null;
            Owns = null;
            Shown = null;
            Name = null;
            Handed = null;
            Started = null;
            Design = null;
            Tag = null;
            Taken = null;
            KeepsLook = null;
            CopyOver = null;
            CopyOnPlot = null;
            CopyRemove = null;
            CopyGone = null;
            PlanningTopic = null;
            Notice = null;
        }

        internal static float WorkOn(SiteState site, float hours)
        {
            try
            {
                return Hours != null ? Mathf.Max(0f, Hours(site, hours)) : hours;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return hours;
            }
        }

        internal static bool IsOwned(SiteState site)
        {
            try
            {
                return Owns != null && site != null && Owns(site);
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return false;
            }
        }

        internal static bool IsShown(SiteState site)
        {
            try
            {
                return Shown == null || site == null || Shown(site);
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return true;
            }
        }

        internal static string NameOf(SiteState site)
        {
            try
            {
                return Name != null && site != null ? Name(site) : null;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return null;
            }
        }

        internal static void HandedOver(SiteState site)
        {
            try
            {
                Handed?.Invoke(site);
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
        }

        internal static void SiteStarted(SiteState site)
        {
            try
            {
                Started?.Invoke(site);
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
        }

        internal static string DesignOf(SiteState site)
        {
            try
            {
                return Design != null ? Design(site) : null;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return null;
            }
        }

        internal static string TagOf(SiteState site)
        {
            try
            {
                return Tag != null ? Tag(site) : null;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return null;
            }
        }

        internal static bool Keeps(SiteState site)
        {
            try
            {
                return KeepsLook != null && KeepsLook(site);
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return false;
            }
        }

        internal static bool IsTaken(string tag)
        {
            try
            {
                return Taken != null && !string.IsNullOrEmpty(tag) && Taken(tag);
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return false;
            }
        }

        internal static DialogEntry NoticeOf(int site)
        {
            try
            {
                return Notice != null ? Notice(site) : null;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return null;
            }
        }

        internal static DialogEntry Planning(Func<DialogEntry> back)
        {
            try
            {
                return PlanningTopic != null ? PlanningTopic(back) : null;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return null;
            }
        }
    }
}
