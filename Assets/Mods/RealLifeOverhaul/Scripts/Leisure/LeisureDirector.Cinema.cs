using System;
using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// The film programme the game lacks: each cinema or theatre shows one film a day, named on the way in, and a show
    /// watched for 45 game minutes or more gets a verdict. The game itself seats the player and passes the time.
    /// </summary>
    public sealed partial class LeisureDirector
    {
        private static readonly string[] Films =
        {
            "reallife_leisure_film_1", "reallife_leisure_film_2", "reallife_leisure_film_3", "reallife_leisure_film_4",
            "reallife_leisure_film_5", "reallife_leisure_film_6", "reallife_leisure_film_7", "reallife_leisure_film_8",
            "reallife_leisure_film_9", "reallife_leisure_film_10", "reallife_leisure_film_11", "reallife_leisure_film_12"
        };
        private static readonly int[] FilmStars = { 2, 1, 0, 2, 1, 1, 0, 2, 1, 2, 0, 1 };   // 2 great, 1 good, 0 a dud

        private Buildings.Building _cinema;
        private int _film = -1;
        private float _watchedFrom = -1f;   // game minute the show began, or -1

        private void Cinema(BuildingManager manager, string business, bool on)
        {
            bool inside = on && Settings.LeisureCinema && manager != null && (business == LeisureVenues.Cinema || business == LeisureVenues.Theater);
            if (!inside)
            {
                _cinema = null;
                _film = -1;
                _watchedFrom = -1f;
                return;
            }
            if (!ReferenceEquals(manager.building, _cinema))
            {
                _cinema = manager.building;
                string name = manager.building.name ?? string.Empty;
                int hash = TimeHelper.CurrentDay * 31;
                for (int i = 0; i < name.Length; i++)
                {
                    hash = hash * 17 + name[i];
                }
                _film = Mathf.Abs(hash % Films.Length);
                _watchedFrom = -1f;
                JobText.Toast(NotificationType.Info, business == LeisureVenues.Cinema ? "reallife_leisure_cinema_showing" : "reallife_leisure_theatre_showing",
                    "film", JobText.T(Films[_film]), 8f);
            }
            bool watching = false;
            List<HappinessModifierData> modifiers = SaveGameManager.Current.happinessModifiers;
            for (int i = 0; modifiers != null && i < modifiers.Count; i++)
            {
                string type = modifiers[i].type;
                if (type != null && type.IndexOf("watching_show", StringComparison.Ordinal) >= 0)
                {
                    watching = true;
                    break;
                }
            }
            float minutes = TimeHelper.NowInMinutes();
            if (watching && _watchedFrom < 0f)
            {
                _watchedFrom = minutes;
            }
            else if (!watching && _watchedFrom >= 0f)
            {
                float watched = minutes - _watchedFrom;
                _watchedFrom = -1f;
                if (watched >= 45f && _film >= 0)
                {
                    int stars = FilmStars[_film];
                    LeisureState.Record("cinema", LeisureState.PlaysOf("cinema") + 1);
                    if (stars > 0)
                    {
                        ModState.AddModifier(stars == 2 ? ModState.MoodUpBig : ModState.MoodUp);
                    }
                    JobText.Toast(NotificationType.Success,
                        stars == 2 ? "reallife_leisure_cinema_great" : stars == 1 ? "reallife_leisure_cinema_good" : "reallife_leisure_cinema_dud",
                        "film", JobText.T(Films[_film]), 9f);
                }
            }
        }
    }
}
