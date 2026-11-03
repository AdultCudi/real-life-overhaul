using System;
using System.Collections.Generic;
using RealLifeOverhaul.Photos;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Gallery on the kit: an album under a heading a day, and one photo with its sidecar. Thumbnails stay on the
    /// worker the album already uses. A picture made for the viewer is destroyed when the viewer closes. Cost: a layout
    /// when the album, the scroll or a thumbnail changes.
    /// </summary>
    sealed class GalleryScreen : IUiScreen
    {
        private const string PicturePrefix = "photo:";

        private static Func<string, Sprite> _next;
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(64);
        private static Texture2D _whole;
        private static Sprite _wholeSprite;
        private static string _viewingName;

        private readonly GalleryApp _app;
        private readonly List<PhotoEntry> _shown = new List<PhotoEntry>(PhotoStoreRules.Cap);
        private readonly List<string> _picked = new List<string>(8);
        private readonly UiAlbumModel _album = new UiAlbumModel();
        private readonly UiPhotoModel _photo = new UiPhotoModel();
        private readonly string[] _tags = new string[4];
        private GalleryAlbum _tab = GalleryAlbum.All;
        private bool _oldest, _fav, _select, _viewing;
        private int _index = -1;
        private int _thumbs = -1;
        private bool _force = true;
        private int _asked = -1;
        private int _window;

        public GalleryScreen(GalleryApp app)
        {
            _app = app;
        }

        public string Id => "photo_gallery";

        public bool Dirty => _force || (_app != null && _app.ThumbsReady != _thumbs);

        /// <summary>Lets go of pictures this screen made. The album's own thumbnails stay the gallery's to destroy.</summary>
        public static void Release()
        {
            foreach (KeyValuePair<string, Sprite> pair in Sprites)
            {
                if (pair.Value != null)
                {
                    UnityEngine.Object.Destroy(pair.Value);
                }
            }
            Sprites.Clear();
            _viewingName = null;
            DropWhole();
            if (UiPictures.Find == Find)
            {
                UiPictures.Find = _next;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Install();
            Build();
            _thumbs = _app != null ? _app.ThumbsReady : 0;
            _force = false;
            string move = UiText.T("reallife_ui_prompt_move");
            string select = UiText.T("reallife_ui_prompt_select");
            string back = UiText.T("reallife_ui_prompt_back");
            LifePages.Gallery(s, frame, dark, keys, UiText.T("reallife_gallery_app"), Nav(), (int)_tab, _viewing, _viewing ? null : _album, _viewing ? _photo : null, move, select, back);
        }

        public bool Press(int command, int arg)
        {
            if (command == UiAppWindow.NavCommand && arg >= 0 && arg < 2)
            {
                _tab = (GalleryAlbum)arg;
                _viewing = false;
                _album.Offset = 0f;
                _force = true;
                return true;
            }
            if (command == UiAlbum.Sort)
            {
                _oldest = !_oldest;
                _force = true;
                return true;
            }
            if (command == UiAlbum.Fav)
            {
                _fav = !_fav;
                _viewing = false;
                _force = true;
                return true;
            }
            if (command == UiAlbum.Select)
            {
                _select = !_select;
                if (!_select)
                {
                    _picked.Clear();
                }
                _force = true;
                return true;
            }
            if (command == UiAlbum.Delete)
            {
                ConfirmDelete();
                return _picked.Count > 0;
            }
            if (command == UiAlbum.Photo && arg >= 0 && arg < _shown.Count)
            {
                if (_select)
                {
                    TogglePick(_shown[arg].Name);
                }
                else
                {
                    _viewing = true;
                    _index = arg;
                }
                _force = true;
                return true;
            }
            if (command == UiPhotoViewer.Step)
            {
                return Step(arg < 0 ? -1 : 1);
            }
            if (command == UiPhotoViewer.Neighbour && _window + arg >= 0 && _window + arg < _shown.Count)
            {
                _index = _window + arg;
                _asked = -1;
                _force = true;
                return true;
            }
            if (command == UiPhotoViewer.Action)
            {
                return ActOn(arg);
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (_viewing)
            {
                _viewing = false;
                DropWhole();
                _force = true;
                return true;
            }
            if (_select)
            {
                _select = false;
                _picked.Clear();
                _force = true;
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            int next = (int)_tab + (step < 0 ? -1 : 1);
            if (next >= 0 && next <= 1)
            {
                _tab = (GalleryAlbum)next;
                _viewing = false;
                _force = true;
            }
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
            _album.Reveal = index;
            _force = true;
        }

        public void Wheel(int clip, float notches)
        {
            _album.Offset = Math.Max(0f, _album.Offset - notches * 48f);
            _force = true;
        }

        private void Build()
        {
            PhotosDirector director = Director;
            _shown.Clear();
            if (director != null)
            {
                GalleryAlbum album = _fav ? GalleryAlbum.Favourites : _tab;
                GalleryRules.Album(director.Photos, album, director.Favourites, _shown, _oldest);
            }
            if (_index >= _shown.Count)
            {
                _index = _shown.Count - 1;
            }
            if (_viewing && _index >= 0)
            {
                FillPhoto(director);
            }
            else
            {
                FillAlbum(director);
            }
        }

        private void FillAlbum(PhotosDirector director)
        {
            int n = _shown.Count;
            if (_album.Photos == null || _album.Photos.Length < n)
            {
                _album.Photos = new UiAlbumPhoto[Math.Max(8, n)];
            }
            _album.Count = n;
            _album.SortLabel = UiText.T(_oldest ? "reallife_ui_life_sort_old" : "reallife_ui_life_sort_new");
            _album.FavLabel = UiText.T("reallife_ui_life_favourites");
            _album.FavOn = _fav;
            _album.SelectLabel = UiText.T("reallife_ui_life_select");
            _album.Selecting = _select;
            _album.DeleteLabel = UiText.T("reallife_ui_life_delete_selected");
            _album.DeleteCount = _picked.Count;
            _album.CountLabel = UiText.Fill("reallife_ui_life_album_count", "count", n.ToString());
            _album.EmptyTitle = UiText.T("reallife_ui_life_empty_gallery_title");
            _album.EmptyBody = UiText.T("reallife_ui_life_empty_gallery_body");
            int i = 0;
            while (i < n)
            {
                int day = _shown[i].Day;
                int count = 0;
                while (i + count < n && _shown[i + count].Day == day)
                {
                    count++;
                }
                string label = UiText.Fill("reallife_ui_life_day_band", "day", day > 0 ? day.ToString() : "0");
                string meta = UiText.Fill("reallife_ui_life_photos_day", "count", count.ToString());
                for (int k = 0; k < count; k++)
                {
                    PhotoEntry photo = _shown[i + k];
                    _album.Photos[i + k] = new UiAlbumPhoto
                    {
                        Picture = PicturePrefix + photo.Name, Day = day, DayLabel = label, DayMeta = meta,
                        Favourite = director != null && director.IsFavourite(photo.Name), Picked = Picked(photo.Name),
                    };
                    int slot = i + k;
                    int focus = (int)(_album.Offset / 160f);
                    if (_app != null && slot >= focus && slot < focus + 24)
                    {
                        _app.WantShown(photo.Name);
                        Texture2D thumb = _app.Thumb(photo.Name);
                        Sprite held;
                        if (thumb != null && (!Sprites.TryGetValue(photo.Name, out held) || held == null))
                        {
                            Sprite made = Sprite.Create(thumb, new Rect(0f, 0f, thumb.width, thumb.height), new Vector2(0.5f, 0.5f), 100f);
                            made.name = "RealLifeOverhaul Thumb";
                            Sprites[photo.Name] = made;
                        }
                    }
                }
                i += count;
            }
        }

        private void FillPhoto(PhotosDirector director)
        {
            PhotoEntry photo = _shown[_index];
            EnsureWhole(director, photo);
            PhotoSidecar side = default;
            if (director != null)
            {
                PhotoSidecarFile.Read(System.IO.Path.Combine(director.Folder, photo.Name), out side);
            }
            string place = string.IsNullOrEmpty(side.Place) ? UiText.T("reallife_ui_life_no_place") : side.Place;
            string night = UiText.Fill("reallife_ui_life_at_night", "place", place);
            _viewingName = photo.Name;
            _photo.Picture = PicturePrefix + photo.Name;
            _photo.Title = PhotoSidecarRules.Title(side, UiText.T("reallife_ui_life_at_night"), place);
            if (string.IsNullOrEmpty(side.Place))
            {
                _photo.Title = place;
            }
            else if (PhotoSidecarRules.Night(side.Hour))
            {
                _photo.Title = night;
            }
            _photo.Caption = PhotosText.Taken(photo);
            int onDay = 0, at = 0;
            for (int i = 0; i < _shown.Count; i++)
            {
                if (_shown[i].Day == photo.Day)
                {
                    onDay++;
                    if (i <= _index)
                    {
                        at++;
                    }
                }
            }
            _photo.Position = UiText.Fill("reallife_ui_life_of_today", "at", at.ToString(), "count", onDay.ToString());
            _photo.FactsTitle = UiText.T("reallife_ui_life_facts");
            _photo.Facts = new[]
            {
                new UiPhotoFact { Label = UiText.T("reallife_ui_life_fact_taken"), Value = PhotosText.Taken(photo) },
                new UiPhotoFact { Label = UiText.T("reallife_ui_life_fact_place"), Value = place, Link = !string.IsNullOrEmpty(side.Place) },
                new UiPhotoFact { Label = UiText.T("reallife_ui_life_fact_camera"), Value = UiText.T(photo.Selfie ? "reallife_ui_life_camera_selfie" : "reallife_ui_life_camera_rear") },
                new UiPhotoFact { Label = UiText.T("reallife_ui_life_fact_filter"), Value = FilterName(side.Filter) },
                new UiPhotoFact { Label = UiText.T("reallife_ui_life_fact_frame"), Value = FrameName(side.Frame) },
                new UiPhotoFact { Label = UiText.T("reallife_ui_life_fact_size"), Value = SizeOf(director, photo) },
            };
            PhotoSidecarRules.Tags(side, UiText.T("reallife_ui_life_tag_night"), WeatherName(side.Weather), _tags);
            _photo.TagsTitle = UiText.T("reallife_ui_life_tags");
            _photo.Tags = _tags;
            _photo.WhereTitle = UiText.T("reallife_ui_life_where");
            _photo.Where = Story(director, photo);
            _photo.CanBack = _index > 0;
            _photo.CanOn = _index + 1 < _shown.Count;
            int neighbours = Math.Min(_shown.Count, 9);
            int first = Math.Max(0, Math.Min(_shown.Count - neighbours, _index - 4));
            _window = first;
            _photo.Neighbours = new string[neighbours];
            for (int i = 0; i < neighbours; i++)
            {
                _photo.Neighbours[i] = PicturePrefix + _shown[first + i].Name;
            }
            _photo.NeighbourAt = _index - first;
            _photo.Actions = new[]
            {
                new UiActionModel { Label = UiText.T("reallife_ui_life_share"), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Icon = UiIcon.Send },
                new UiActionModel { Label = UiText.T("reallife_ui_life_wallpaper"), Kind = UiButtonKind.Secondary, Key = UiAction.Option },
                new UiActionModel { Icon = UiIcon.Heart, Kind = UiButtonKind.Secondary },
                new UiActionModel { Icon = UiIcon.Trash, Kind = UiButtonKind.Destructive },
            };
        }

        private void EnsureWhole(PhotosDirector director, PhotoEntry photo)
        {
            if (director == null || _asked == _index)
            {
                return;
            }
            _asked = _index;
            DropWhole();
            string path = System.IO.Path.Combine(director.Folder, photo.Name);
            int index = _index;
            director.StartCoroutine(Guard.Routine(PhotosGame.Load(path, made =>
            {
                if (made == null || !_viewing || _index != index)
                {
                    if (made != null)
                    {
                        UnityEngine.Object.Destroy(made);
                    }
                    return;
                }
                DropWhole();
                _whole = made;
                _wholeSprite = Sprite.Create(made, new Rect(0f, 0f, made.width, made.height), new Vector2(0.5f, 0.5f), 100f);
                _wholeSprite.name = "RealLifeOverhaul Photo";
                _force = true;
            }, () => _viewing && _index == index)));
        }

        private static void DropWhole()
        {
            if (_wholeSprite != null)
            {
                UnityEngine.Object.Destroy(_wholeSprite);
                _wholeSprite = null;
            }
            if (_whole != null)
            {
                UnityEngine.Object.Destroy(_whole);
                _whole = null;
            }
        }

        private bool Step(int delta)
        {
            int to = _index + delta;
            if (!_viewing || to < 0 || to >= _shown.Count)
            {
                return false;
            }
            _index = to;
            _asked = -1;
            _force = true;
            return true;
        }

        private bool ActOn(int arg)
        {
            if (_index < 0 || _index >= _shown.Count)
            {
                return false;
            }
            PhotoEntry photo = _shown[_index];
            PhotosDirector director = Director;
            if (director == null)
            {
                return false;
            }
            if (arg == 0)
            {
                string path = System.IO.Path.Combine(director.Folder, photo.Name);
                if (PhotosBridges.ShareToFlare == null || !PhotosBridges.ShareToFlare(path, PhotoThumbs.FileOf(path)))
                {
                    PhotosGame.Tell(NotificationType.Warning, "reallife_gallery_note_flare_off");
                }
                return true;
            }
            if (arg == 1)
            {
                director.SetWallpaper(photo.Name == director.WallpaperName ? null : photo.Name);
                _force = true;
                return true;
            }
            if (arg == 2)
            {
                director.ToggleFavourite(photo.Name);
                _force = true;
                return true;
            }
            if (arg == 3)
            {
                string name = photo.Name;
                GameConfirm.Show("reallife_gallery_delete_title", "reallife_gallery_delete_body", () =>
                {
                    director.StartCoroutine(Guard.Routine(director.Delete(new List<string> { name }, 0)));
                    _viewing = false;
                    _force = true;
                }, null, "reallife_gallery_delete_yes", "common_cancel", true, true);
                return true;
            }
            return false;
        }

        private void ConfirmDelete()
        {
            PhotosDirector director = Director;
            if (director == null || _picked.Count == 0)
            {
                return;
            }
            var names = new List<string>(_picked);
            GameConfirm.Show("reallife_gallery_delete_title", "reallife_gallery_delete_body", () =>
            {
                director.StartCoroutine(Guard.Routine(director.Delete(names, 0)));
                _picked.Clear();
                _select = false;
                _force = true;
            }, null, "reallife_gallery_delete_yes", "common_cancel", true, true);
        }

        private void TogglePick(string name)
        {
            for (int i = 0; i < _picked.Count; i++)
            {
                if (_picked[i] == name)
                {
                    _picked.RemoveAt(i);
                    return;
                }
            }
            _picked.Add(name);
        }

        private bool Picked(string name)
        {
            for (int i = 0; i < _picked.Count; i++)
            {
                if (_picked[i] == name)
                {
                    return true;
                }
            }
            return false;
        }

        private PhotosDirector Director => PhotosDirector.Instance;

        private UiNavEntry[] Nav()
        {
            int all = Director != null ? Director.Count : 0;
            int selfies = 0;
            if (Director != null)
            {
                for (int i = 0; i < Director.Photos.Count; i++)
                {
                    selfies += Director.Photos[i].Selfie ? 1 : 0;
                }
            }
            return new[]
            {
                new UiNavEntry { Icon = UiIcon.Image, Label = UiText.T("reallife_gallery_tab_all"), Count = all.ToString() },
                new UiNavEntry { Icon = UiIcon.Camera, Label = UiText.T("reallife_gallery_tab_selfies"), Count = selfies.ToString() },
            };
        }

        private static string FilterName(int filter)
        {
            filter = CameraRules.ClampFilter(filter);
            return UiText.T(CameraRules.Filters[filter].NameKey);
        }

        private static string FrameName(int frame)
        {
            frame = (int)CameraRules.ClampFrame(frame);
            return frame >= 0 && frame < CameraRules.FrameKeys.Length ? UiText.T(CameraRules.FrameKeys[frame]) : UiText.T(CameraRules.FrameKeys[0]);
        }

        private static string WeatherName(string token)
        {
            switch (token)
            {
                case "rain": return UiText.T("reallife_ui_hud_weather_rain");
                case "mist": return UiText.T("reallife_ui_hud_weather_mist");
                case "storm": return UiText.T("reallife_ui_hud_weather_storm");
                case "snow": return UiText.T("reallife_ui_hud_weather_snow");
                case "clear": return UiText.T("reallife_ui_hud_weather_clear");
                default: return UiText.T("reallife_ui_hud_weather_sunny");
            }
        }

        private static string SizeOf(PhotosDirector director, PhotoEntry photo)
        {
            if (director == null || string.IsNullOrEmpty(photo.Name))
            {
                return "";
            }
            string path = System.IO.Path.Combine(director.Folder, photo.Name);
            long bytes = 0;
            try
            {
                if (System.IO.File.Exists(path))
                {
                    bytes = new System.IO.FileInfo(path).Length;
                }
            }
            catch (System.IO.IOException)
            {
                bytes = 0;
            }
            if (bytes <= 0)
            {
                return "";
            }
            return (bytes / 1024).ToString() + " KB";
        }

        private static string Story(PhotosDirector director, PhotoEntry photo)
        {
            Flare.FlareDirector flare = Flare.FlareDirector.Instance;
            if (flare == null || !flare.PictureStory(photo.Name, out string business, out int likes, out int came, out long cents))
            {
                return null;
            }
            string money = (cents >= 0 ? "+" : "") + (cents / 100).ToString();
            return UiText.Fill("reallife_ui_life_flare_line", "name", string.IsNullOrEmpty(business) ? UiText.T("reallife_ui_life_a_business") : business,
                "likes", likes.ToString(), "visits", came.ToString(), "money", money);
        }

        private void Install()
        {
            if (UiPictures.Find != Find)
            {
                _next = UiPictures.Find;
                UiPictures.Find = Find;
            }
        }

        private static Sprite Find(string key)
        {
            if (key != null && key.StartsWith(PicturePrefix, StringComparison.Ordinal))
            {
                string name = key.Substring(PicturePrefix.Length);
                if (_wholeSprite != null && _viewingName != null && name == _viewingName)
                {
                    return _wholeSprite;
                }
                Sprite cached;
                if (Sprites.TryGetValue(name, out cached) && cached != null)
                {
                    return cached;
                }
            }
            return _next != null ? _next(key) : null;
        }
    }
}
