namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// Whether the minimap's picture drew nothing, kept for the session: the first draw that comes out black says the colour shader does not draw here (or the camera's
    /// matrices put the whole city out of view), and once that is said the minimap is left flat. The option or a new city does not clear it: it is the machine's answer. The
    /// first word of it is told to the log once. Game-free.
    /// </summary>
    public static class NavMapLatch
    {
        private static bool _black;

        /// <summary>Whether the picture drew nothing earlier in this session.</summary>
        public static bool Drew => _black;

        /// <summary>The picture came out black: true the first time (the caller says so to the log), false after.</summary>
        public static bool Mark()
        {
            if (_black)
            {
                return false;
            }
            _black = true;
            return true;
        }

        /// <summary>For the checks only.</summary>
        public static void Forget()
        {
            _black = false;
        }
    }
}
