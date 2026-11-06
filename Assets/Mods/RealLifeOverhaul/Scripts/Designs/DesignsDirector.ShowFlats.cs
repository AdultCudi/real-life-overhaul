using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Designs
{
    /// <summary>
    /// The show flats' designs (ShowFlatDesigns) for the construction game: read from the game's residential designs on the first
    /// ask of a session and kept, as the game's files do not change while it runs. Main thread, on a press or a show flat's floor
    /// asked again at a load; nothing per frame, saved or sent.
    /// </summary>
    public static partial class DesignsDirector
    {
        private static readonly List<ShowDesign> Shows = new List<ShowDesign>(16);
        private static bool _showsRead;

        /// <summary>
        /// The designs a show flat of flats of a floor area is offered: those of the apartment structure whose storey is nearest
        /// that area, staged first, at most <see cref="ShowFlatDesigns.Most"/>. Into the caller's list (cleared); false for none.
        /// </summary>
        public static bool ShowDesigns(int flatArea, List<ShowDesign> into)
        {
            into.Clear();
            ReadShows();
            var sizes = new List<string>(4);
            var sqm = new List<int>(4);
            for (int i = 0; i < Shows.Count; i++)
            {
                if (!sizes.Contains(Shows[i].Folder))
                {
                    sizes.Add(Shows[i].Folder);
                    sqm.Add(Shows[i].Sqm);
                }
            }
            int nearest = ShowFlatDesigns.Nearest(sqm, flatArea);
            if (nearest < 0)
            {
                return false;
            }
            for (int f = 0; f < 2; f++)
            {
                for (int i = 0; i < Shows.Count && into.Count < ShowFlatDesigns.Most; i++)
                {
                    if (Shows[i].Folder == sizes[nearest] && (int)Shows[i].Finish == f)
                    {
                        into.Add(Shows[i]);
                    }
                }
            }
            return into.Count > 0;
        }

        /// <summary>A show flat's design by its id, or null for one the game's files no longer give.</summary>
        public static ShowDesign FindShow(string id)
        {
            ReadShows();
            for (int i = 0; i < Shows.Count; i++)
            {
                if (string.Equals(Shows[i].Id, id, StringComparison.Ordinal))
                {
                    return Shows[i];
                }
            }
            return null;
        }

        private static void ReadShows()
        {
            if (_showsRead)
            {
                return;
            }
            _showsRead = true;
            try
            {
                DesignsGame.ShowDesigns(Shows);
                Logger?.Info("Designs: " + Shows.Count + " show flat design(s) from the game's residential designs.");
            }
            catch (Exception e)
            {
                Shows.Clear();
                Logger?.Error(e);
                Logger?.Warn("Designs: the game's residential designs could not be read; no show flat design is offered this session.");
            }
        }
    }
}
