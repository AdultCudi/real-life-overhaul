using System;
using RealLifeOverhaul.Gfx;
using RealLifeOverhaul.Seasons;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The picture (Graphics/GraphicsBridges) with the seasons: the season the city shows, and the snow that falls and
        /// lies where the player can see it, so a look is cooler and paler in winter and its shade blue over snow. With the
        /// seasons off, standing down for another mod's, or their snow not drawn, the picture knows of neither. Asked when
        /// the picture is worked out again, never per frame; a fault is said once and the bridge's default answered.
        /// </summary>
        public static class GraphicsLinks
        {
            internal static void Connect()
            {
                GraphicsBridges.Season = Season;
                GraphicsBridges.SnowFalling = SnowFalling;
                GraphicsBridges.SnowCover = SnowCover;
            }

            internal static void Disconnect()
            {
                GraphicsBridges.Reset();
            }

            private static int Season()
            {
                try
                {
                    SeasonDirector director = SeasonsOn();
                    return director != null ? director.Season : -1;
                }
                catch (Exception e)
                {
                    Fault("GraphicsLinks.Season", e);
                    return -1;
                }
            }

            private static float SnowFalling()
            {
                try
                {
                    SeasonDirector director = SeasonsOn();
                    return director != null && Settings.SnowLook ? director.Falling : 0f;
                }
                catch (Exception e)
                {
                    Fault("GraphicsLinks.SnowFalling", e);
                    return 0f;
                }
            }

            private static float SnowCover()
            {
                try
                {
                    SeasonDirector director = SeasonsOn();
                    return director != null && Settings.SnowLook ? director.Cover : 0f;
                }
                catch (Exception e)
                {
                    Fault("GraphicsLinks.SnowCover", e);
                    return 0f;
                }
            }

            // The seasons' director while its seasons are on the city: it works its season and its snow out only then.
            private static SeasonDirector SeasonsOn()
            {
                SeasonDirector director = SeasonDirector.Instance;
                return director != null && Settings.Seasons && !SeasonDirector.StandingDown ? director : null;
            }
        }
    }
}
