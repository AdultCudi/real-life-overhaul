using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// "You could build here" in the Place tool's panel while nothing is picked: the player's empty plots and the lots for sale with
    /// what fits each (ConstructionDirector.FitHere), a press taking the map's camera to it. Read once for a picture of the land and
    /// again after an action; nothing a frame.
    /// </summary>
    public sealed partial class BuildMode
    {
        private const int RowFit = 200;      // a site of the list: RowFit plus its index, under RowMark
        private const int FitMost = 5;
        private readonly List<FitSite> _fits = new List<FitSite>(FitMost);
        private bool _fitsRead;

        private void WriteFits(IBuildSheet s)
        {
            IReadOnlyList<FitSite> list = FitList;
            if (list.Count == 0)
            {
                return;
            }
            s.Line(T("reallife_bld_fit_title"), BuildModeBar.Text);
            for (int i = 0; i < list.Count; i++)
            {
                s.Site(RowFit + i, list[i].Name, list[i].Fits, list[i].Mine);
            }
        }

        // A site of the list pressed: the map glides to it.
        private bool ActFit(int id)
        {
            int i = id - RowFit;
            if (i < 0 || i >= _fits.Count)
            {
                return false;
            }
            BuildModeGame.FlyTo(new UnityEngine.Vector3(_fits[i].X, 0f, _fits[i].Z));
            return true;
        }

        /// <summary>The list of "You could build here" as read now (read again after an action), for the panel and the tests.</summary>
        public IReadOnlyList<FitSite> FitList
        {
            get
            {
                if (!_fitsRead)
                {
                    _fitsRead = true;
                    _d.FitHere(_fits, FitMost);
                }
                return _fits;
            }
        }
    }
}
