using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The finished buildings' new looks in the street: every look the works state keeps is stood through the copy engine
    /// (SiteBuilders.CopyOver for a rebuilt building, CopyOnPlot for a plot's finished building), out of view in play and
    /// behind the loading screen at a load; a plot's look is given up once its lot holds a building of the district, which
    /// then stands there itself. Nothing per frame: asked at a load, a handover, the host's line and the option.
    /// </summary>
    public sealed class SiteLooks
    {
        private readonly WorksState _state;
        private readonly List<SiteLook> _stood = new List<SiteLook>(8);
        private readonly List<object> _handles = new List<object>(8);

        public SiteLooks(WorksState state)
        {
            _state = state;
        }

        /// <summary>Copies standing or waiting to.</summary>
        public int Standing => _handles.Count;

        /// <summary>
        /// Every look of the works state stood (those standing are kept), a copy whose look has left the state taken away, and
        /// a plot's look whose lot holds a building of the district now dropped. With <paramref name="again"/> (a load, the
        /// option) a copy the engine took away or refused is asked for once more; otherwise (the host's hourly line) it is not.
        /// Returns how many were asked for.
        /// </summary>
        public int StandAll(bool again)
        {
            for (int i = _state.Looks.Count - 1; i >= 0; i--)
            {
                if (_state.Looks[i].Kind == SiteKind.Plot && SiteBuilders.IsTaken(_state.Looks[i].Tag))
                {
                    CivicDirector.Logger?.Info("Construction: the finished building on lot " + _state.Looks[i].Tag + " is the district's own now: its look is no longer kept.");
                    _state.Looks.RemoveAt(i);
                }
            }
            for (int i = _stood.Count - 1; i >= 0; i--)
            {
                if (!Kept(_stood[i]) || (again && Gone(_handles[i])))
                {
                    RemoveAt(i);
                }
            }
            int asked = 0;
            for (int i = 0; i < _state.Looks.Count; i++)
            {
                if (IndexOf(_state.Looks[i]) < 0 && Stand(_state.Looks[i]))
                {
                    asked++;
                }
            }
            return asked;
        }

        /// <summary>One look stood (a handover): a copy of the same building or ground already standing goes first.</summary>
        public bool Stand(SiteLook look)
        {
            int at = IndexOf(look);
            if (at >= 0)
            {
                RemoveAt(at);
            }
            object handle = Ask(look);
            if (handle == null)
            {
                return false;
            }
            _stood.Add(look);
            _handles.Add(handle);
            return true;
        }

        /// <summary>Every copy taken away (the option off, the city going); the looks stay kept for the next time.</summary>
        public void Clear()
        {
            for (int i = _stood.Count - 1; i >= 0; i--)
            {
                RemoveAt(i);
            }
        }

        private static object Ask(SiteLook look)
        {
            if (look.Kind == SiteKind.Rebuild)
            {
                CityBuildingController building = CivicCity.Find(look.Street, look.Number);
                return building != null && SiteBuilders.CopyOver != null ? SiteBuilders.CopyOver(building, look.Design) : null;
            }
            if (SiteBuilders.CopyOnPlot == null || !PlotRules.TryRead(look.Street, out int width, out int depth, out int yaw, out _))
            {
                return null;
            }
            // The finished building stands where the frame stood: the frame's footprint, its middle in the world.
            PlotRules.Frame(width, depth, out float minX, out float maxX, out float minZ, out float maxZ);
            PlotRules.ToWorld(look.X, look.Z, yaw, (minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f, out float x, out float z);
            int frameWidth = Mathf.Max(1, Mathf.RoundToInt(maxX - minX)), frameDepth = Mathf.Max(1, Mathf.RoundToInt(maxZ - minZ));
            return SiteBuilders.CopyOnPlot(look.Design, new Vector3(x, look.Y, z), yaw, frameWidth, frameDepth);
        }

        private bool Kept(SiteLook look)
        {
            for (int i = 0; i < _state.Looks.Count; i++)
            {
                if (ReferenceEquals(_state.Looks[i], look) || _state.Looks[i].Same(look) && _state.Looks[i].Design == look.Design)
                {
                    return true;
                }
            }
            return false;
        }

        private int IndexOf(SiteLook look)
        {
            for (int i = 0; i < _stood.Count; i++)
            {
                if (_stood[i].Same(look) && _stood[i].Design == look.Design)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool Gone(object handle)
        {
            try
            {
                return SiteBuilders.CopyGone == null || SiteBuilders.CopyGone(handle);
            }
            catch (System.Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return true;
            }
        }

        private void RemoveAt(int i)
        {
            object handle = _handles[i];
            _stood.RemoveAt(i);
            _handles.RemoveAt(i);
            try
            {
                SiteBuilders.CopyRemove?.Invoke(handle);
            }
            catch (System.Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
        }
    }
}
