using System;
using System.Reflection;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The buying screens' one read of food spoilage: the days a product keeps once it is stocked, as the spoilage rules give
    /// them (Economy.LogisticsDirector.ShelfLife). The method is found by its name once; while a build has no spoilage, or it is
    /// off, every read is -1 and the screens show nothing of it.
    /// </summary>
    internal static class SpoilRead
    {
        private const string Director = "RealLifeOverhaul.Economy.LogisticsDirector", Method = "ShelfLife";

        private static Func<string, bool, int> _keeps;
        private static bool _sought;

        /// <summary>Days a product keeps out of the cold, or in a fridge or freezer; -1 when it never spoils, spoilage is off or not in this build.</summary>
        internal static int Keeps(string item, bool cold)
        {
            if (!_sought)
            {
                _sought = true;
                _keeps = Find();
            }
            if (_keeps == null || string.IsNullOrEmpty(item))
            {
                return -1;
            }
            try
            {
                int days = _keeps(item, cold);
                return days > 0 ? days : -1;
            }
            catch (Exception e)
            {
                _keeps = null;   // not asked again this session
                UiDirector.Logger?.Warn("UI: the buying screens leave out how long goods keep (" + e.GetType().Name + ")");
                return -1;
            }
        }

        private static Func<string, bool, int> Find()
        {
            Type director = typeof(SpoilRead).Assembly.GetType(Director, false);
            MethodInfo m = director != null
                ? director.GetMethod(Method, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(bool) }, null) : null;
            if (m == null || m.ReturnType != typeof(int))
            {
                return null;
            }
            return Delegate.CreateDelegate(typeof(Func<string, bool, int>), m, false) as Func<string, bool, int>;
        }
    }
}
