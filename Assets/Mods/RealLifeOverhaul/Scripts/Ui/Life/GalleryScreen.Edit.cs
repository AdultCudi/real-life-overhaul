using System.Threading.Tasks;
using RealLifeOverhaul.Photos;
using Unity.Collections;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Gallery's editor: a photo opened for a filter, a frame, a turn and a shape after the shot. Its pixels are copied out of a
    /// readable decode once (about 6 MB, outside the managed heap); the preview is made on a worker from them (720 pixels, a few
    /// milliseconds) each time a choice changes, the latest choice winning; Save works the whole photo on a worker and writes it as a
    /// new photo, the original untouched. Everything is let go when the editor closes. Cost: a decode and a copy when it opens, a worker
    /// a choice.
    /// </summary>
    sealed partial class GalleryScreen
    {
        private const int PreviewLong = 720;   // pixels: the editor's preview, on its longest side

        private readonly UiPhotoEditModel _editModel = new UiPhotoEditModel();
        private bool _editing, _loadingEdit, _previewing, _previewAgain, _saving;
        private PhotosGame.Edit _edit;
        private PhotoEntry _editOf;
        private Task _previewWork;
        private int _eFilter, _eFrame, _eShape, _eQuarters;   // the filter, the frame, the shape (0 keeps it, then the four) and the quarters turned

        // Opens the editor on a photo: the file decoded readable, its pixels copied, the decode destroyed.
        private void BeginEdit(PhotosDirector director, PhotoEntry photo)
        {
            if (_loadingEdit || _editing)
            {
                return;
            }
            if (PhotoStoreRules.Full(director.Count))
            {
                PhotosGame.Tell(NotificationType.Warning, "reallife_gallery_note_edit_full", "cap", PhotosText.N(PhotoStoreRules.Cap));
                return;
            }
            _loadingEdit = true;
            string path = System.IO.Path.Combine(director.Folder, photo.Name);
            director.StartCoroutine(Guard.Routine(PhotosGame.Load(path, made =>
            {
                _loadingEdit = false;
                PhotosGame.Edit edit = made != null ? PhotosGame.TakeEdit(made) : null;
                if (made != null)
                {
                    UnityEngine.Object.Destroy(made);
                }
                if (edit == null || !_viewing)
                {
                    PhotosGame.Drop(edit);
                    if (_viewing)
                    {
                        PhotosGame.Tell(NotificationType.Warning, "reallife_gallery_note_edit_failed");
                    }
                    return;
                }
                _edit = edit;
                _editOf = photo;
                _editing = true;
                _eFilter = _eFrame = _eShape = _eQuarters = 0;
                RunPreview();
                _force = true;
            }, () => _viewing, true)));
        }

        // The editor's words and choices, written into its model.
        private void FillEdit()
        {
            UiPhotoEditModel m = _editModel;
            m.Title = null;   // the window's own strip and the Edit button say it
            m.FilterTitle = UiText.T("reallife_phonecam_filter_title");
            m.FrameTitle = UiText.T("reallife_phonecam_frame_title");
            m.ShapeTitle = UiText.T("reallife_gallery_fact_shape");
            if (m.Filters.Length != CameraRules.Filters.Length)
            {
                m.Filters = new string[CameraRules.Filters.Length];
                for (int i = 0; i < m.Filters.Length; i++)
                {
                    m.Filters[i] = UiText.T(CameraRules.Filters[i].NameKey);
                }
                m.Frames = new string[CameraRules.FrameKeys.Length];
                for (int i = 0; i < m.Frames.Length; i++)
                {
                    m.Frames[i] = UiText.T(CameraRules.FrameKeys[i]);
                }
                m.Shapes = new[]
                {
                    UiText.T("reallife_gallery_shape_original"), UiText.T(CameraRules.AspectKeys[(int)CameraAspect.FourThree]), UiText.T(CameraRules.AspectKeys[(int)CameraAspect.SixteenNine]),
                    UiText.T(CameraRules.AspectKeys[(int)CameraAspect.Square]), UiText.T(CameraRules.AspectKeys[(int)CameraAspect.Story]),
                };
            }
            m.Filter = _eFilter;
            m.Frame = _eFrame;
            m.Shape = _eShape;
            m.TurnLabel = UiText.T("reallife_gallery_edit_turn");
            m.SaveLabel = UiText.T("reallife_gallery_edit_save");
            m.CancelLabel = UiText.T("common_cancel");
            m.ResetLabel = UiText.T("reallife_gallery_edit_reset");
            m.Caption = UiText.T("reallife_gallery_edit_note");
            m.Busy = _previewing || _saving;
            m.Picture = GalleryPictures.EditKey;
        }

        private bool EditPress(int command, int arg)
        {
            switch (command)
            {
                case UiPhotoEdit.Filter: _eFilter = CameraRules.ClampFilter(arg); break;
                case UiPhotoEdit.Frame: _eFrame = (int)CameraRules.ClampFrame(arg); break;
                case UiPhotoEdit.Shape: _eShape = arg < 0 || arg > 4 ? 0 : arg; break;
                case UiPhotoEdit.Turn: _eQuarters = (_eQuarters + 1) % 4; break;
                case UiPhotoEdit.Reset: _eFilter = _eFrame = _eShape = _eQuarters = 0; break;
                case UiPhotoEdit.Cancel:
                    LeaveEdit();
                    return true;
                case UiPhotoEdit.Save:
                    return SaveEdit();
                default:
                    return false;
            }
            RunPreview();
            _force = true;
            return true;
        }

        private PhotosGame.Edit Snapshot(PhotosGame.Edit edit, NativeArray<byte> pixels)
        {
            return new PhotosGame.Edit
            {
                Pixels = pixels, Width = edit.Width, Height = edit.Height, Filter = _eFilter, Frame = (CameraFrame)_eFrame, Quarters = _eQuarters,
                Crop = _eShape == 0 ? CameraAspect.Unknown : (CameraAspect)_eShape,
            };
        }

        // The preview made on a worker from the choices now; a choice made meanwhile runs it again when this one is done.
        private void RunPreview()
        {
            PhotosDirector director = Director;
            if (_edit == null || director == null)
            {
                return;
            }
            if (_previewing)
            {
                _previewAgain = true;
                return;
            }
            _previewing = true;
            PhotosGame.Edit snap = Snapshot(_edit, _edit.Pixels);
            int w = 0, h = 0;
            Task<byte[]> work = Task.Run(() => PhotosGame.Preview(snap, PreviewLong, out w, out h));
            _previewWork = work;
            director.StartCoroutine(Guard.Routine(PreviewDone(work, () => (w, h))));
        }

        private System.Collections.IEnumerator PreviewDone(Task<byte[]> work, System.Func<(int, int)> size)
        {
            while (!work.IsCompleted)
            {
                yield return null;
            }
            _previewing = false;
            if (!_editing)
            {
                yield break;
            }
            byte[] rows = work.Status == TaskStatus.RanToCompletion ? work.Result : null;
            if (rows != null)
            {
                (int w, int h) = size();
                GalleryPictures.SetPreview(rows, w, h);
            }
            if (_previewAgain)
            {
                _previewAgain = false;
                RunPreview();
            }
            _force = true;
        }

        // Saved as a new photo beside the original: the whole photo worked on a worker from a copy of the pixels, so the editor goes on if it fails.
        private bool SaveEdit()
        {
            PhotosDirector director = Director;
            if (_edit == null || director == null || _saving || _previewing || _editOf == null)
            {
                return false;
            }
            _saving = true;
            var copy = Snapshot(_edit, new NativeArray<byte>(_edit.Pixels, Allocator.Persistent));
            PhotoEntry of = _editOf;
            director.StartCoroutine(Guard.Routine(director.SaveEdit(of, copy, name =>
            {
                _saving = false;
                if (name == null)
                {
                    PhotosGame.Tell(NotificationType.Warning, "reallife_gallery_note_edit_failed");
                    _force = true;
                    return;
                }
                PhotosGame.Tell(NotificationType.Info, "reallife_gallery_note_edit_saved");
                LeaveEdit();
                _rebuild = true;
                GalleryFind.Select(director.Photos, _query, director.Favourites, director.Posted, _shown);
                int at = GalleryRules.IndexOf(_shown, name);
                _index = at >= 0 ? at : _index;
                _asked = -1;
                _force = true;
            })));
            _force = true;
            return true;
        }

        private void LeaveEdit()
        {
            DropEdit();
            _editing = false;
            _force = true;
        }

        // The editor's pixels and preview let go; the pixels outlive a preview still being made, which lets them go when it ends.
        private void DropEdit()
        {
            GalleryPictures.DropPreview();
            PhotosGame.Edit edit = _edit;
            Task work = _previewWork;
            _edit = null;
            _editOf = null;
            _previewAgain = false;
            if (edit != null)
            {
                if (work != null && !work.IsCompleted)
                {
                    work.ContinueWith(_ => PhotosGame.Drop(edit));
                }
                else
                {
                    PhotosGame.Drop(edit);
                }
            }
            _previewWork = null;
            _editing = false;
            _loadingEdit = false;
            _saving = false;
            _previewing = false;
        }
    }
}
