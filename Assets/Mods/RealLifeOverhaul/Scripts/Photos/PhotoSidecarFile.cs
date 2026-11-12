using System.Text;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// Writes and reads a photo's sidecar beside its JPG. Called when the photo is taken and when the Gallery opens
    /// one photo, never from a frame. The file is local to that photo.
    /// </summary>
    public static class PhotoSidecarFile
    {
        /// <summary>Writes the sidecar beside the JPG. False if it could not be written.</summary>
        public static bool Write(string photoPath, PhotoSidecar sidecar)
        {
            string path = PhotoSidecarRules.PathOf(photoPath);
            if (path == null)
            {
                return false;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(PhotoSidecarRules.Format(sidecar));
            return PhotoStore.WriteWhole(path, bytes, bytes.Length);
        }

        /// <summary>Reads the sidecar beside the JPG. False if there is none.</summary>
        public static bool Read(string photoPath, out PhotoSidecar sidecar)
        {
            sidecar = default;
            string path = PhotoSidecarRules.PathOf(photoPath);
            if (path == null || !System.IO.File.Exists(path))
            {
                return false;
            }
            try
            {
                return PhotoSidecarRules.Parse(System.IO.File.ReadAllText(path), out sidecar);
            }
            catch (System.Exception e) when (e is System.IO.IOException || e is System.UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
