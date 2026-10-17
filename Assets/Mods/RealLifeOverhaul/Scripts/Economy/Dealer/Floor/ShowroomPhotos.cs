using System;
using System.Collections.Generic;
using BigAmbitions.SaveSystem;
using Helpers;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// A car's picture for the Stock tab: the game's icon of its type, else a photo taken once a type and colour while such a car
    /// is on the floor, else the game's vehicle picture. Nothing per frame: a photo is a few small renders.
    /// </summary>
    internal static class ShowroomPhotos
    {
        private const int Wide = 384;            // pixels of a photo taken, across
        private const int High = 216;            // ...and up
        private const int Renders = 3;           // renders of one photo: the last is kept, once the exposure has settled
        private const float Lens = 30f;          // degrees of the camera's field of view
        private const float BoxWide = 480f;      // units of the box a picture is shown at, at most, across
        private const float BoxHigh = 250f;      // ...and up

        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Texture2D> Shots = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        /// <summary>The car's picture in the box: the game's icon of its type, its photo, or the game's own vehicle picture. Nothing if none can be had.</summary>
        public static void Show(InfoBox box, StockCar car)
        {
            if (box == null || car == null || string.IsNullOrEmpty(car.Type))
            {
                return;
            }
            try
            {
                Sprite icon = Icon(car.Type);
                if (icon != null)
                {
                    Put(box, icon);
                    return;
                }
                Texture2D shot = Shot(car);
                if (shot != null)
                {
                    Fit(box.Picture(shot, BoxWide, BoxHigh), shot.width, shot.height, new Rect(0f, 0f, 1f, 1f));
                    return;
                }
                GlobalReferences refs = InstanceBehavior<GlobalReferences>.Instance;
                if (refs != null && refs.vehiclePOIIcon != null)
                {
                    Put(box, refs.vehiclePOIIcon);
                }
            }
            catch (Exception e)
            {
                DealerApp.Logger?.Warn("Car Showroom: no picture of the " + car.Type + " could be shown (" + e.Message + "); its page shows without one.");
            }
        }

        // The game's own icon of a vehicle type, if it has one: asked once a type, then remembered.
        private static Sprite Icon(string type)
        {
            if (Icons.TryGetValue(type, out Sprite icon))
            {
                return icon;
            }
            icon = AddressableResolver.IsValidAddressableKey(PlayerHelper.GetItemNameIconPath(type)) ? ItemHelper.GetIcon(type) : null;
            Icons[type] = icon;
            return icon;
        }

        private static void Put(InfoBox box, Sprite sprite)
        {
            Texture texture = sprite.texture;
            if (texture == null)
            {
                return;
            }
            Rect r = sprite.textureRect;
            var uv = new Rect(r.x / texture.width, r.y / texture.height, r.width / texture.width, r.height / texture.height);
            Fit(box.Picture(texture, BoxWide, BoxHigh), r.width, r.height, uv);
        }

        // The picture's part of its texture, at the shape of that part, within the box's room for it.
        private static void Fit(RawImage image, float width, float height, Rect uv)
        {
            if (image == null || width <= 0f || height <= 0f)
            {
                return;
            }
            image.uvRect = uv;
            float high = Mathf.Min(BoxHigh, BoxWide * height / width);
            LayoutElement layout = image.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredHeight = high;
                layout.preferredWidth = high * width / height;
            }
        }

        // The photo of the car's type and colour: taken now if the car is on the floor and none was, else none.
        private static Texture2D Shot(StockCar car)
        {
            string key = car.Type + "|" + (car.Colour ?? string.Empty);
            if (Shots.TryGetValue(key, out Texture2D shot) && shot != null)
            {
                return shot;
            }
            VehicleController controller = DealerGame.Controller(car.VehicleId);
            if (controller == null || !controller.isActiveAndEnabled)
            {
                return null;
            }
            shot = Take(controller);
            if (shot != null)
            {
                Shots[key] = shot;
                DealerApp.Logger?.Info("Car Showroom: a photo of the " + car.Type + " in " + (car.Colour ?? "its colour") + " taken for the app (" + Shots.Count + " this session).");
            }
            return shot;
        }

        // A photo of a car where it stands: its front three quarters, a little from above, framed to its body.
        private static Texture2D Take(VehicleController car)
        {
            Camera main = GameManager.GetMainCamera();
            if (!ShowroomGame.Body(car, out Vector3 middle, out float length))
            {
                return null;
            }
            float radius = Mathf.Max(1.5f, length * 0.55f);
            float yaw = car.transform.eulerAngles.y + FloorCatalogue.YawStart;
            ShowroomFloorRules.Orbit(yaw, FloorCatalogue.PitchStart, radius / Mathf.Sin(Lens * 0.5f * Mathf.Deg2Rad), out float x, out float y, out float z);
            var made = new GameObject("RealLifeOverhaul Showroom photo");
            made.SetActive(false);
            GameObject light = null;
            RenderTexture target = null;
            RenderTexture before = RenderTexture.active;
            Texture2D shot = null;
            VolumeProfile profile = null;
            try
            {
                Camera camera = made.AddComponent<Camera>();
                if (main != null)
                {
                    camera.CopyFrom(main);
                }
                HDAdditionalCameraData data = made.AddComponent<HDAdditionalCameraData>();
                HDAdditionalCameraData mainData = main != null ? main.GetComponent<HDAdditionalCameraData>() : null;
                if (mainData != null)
                {
                    mainData.CopyTo(data);
                }
                camera.enabled = false;
                camera.fieldOfView = Lens;
                camera.transform.SetPositionAndRotation(middle + new Vector3(x, y, z), Quaternion.LookRotation(-new Vector3(x, y, z), Vector3.up));
                target = RenderTexture.GetTemporary(Wide, High, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                camera.targetTexture = target;
                camera.aspect = Wide / (float)High;

                // The game's automatic exposure, made to settle at once for these renders only.
                light = new GameObject("RealLifeOverhaul Showroom photo exposure");
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                Exposure exposure = profile.Add<Exposure>(true);
                exposure.mode.Override(ExposureMode.Automatic);
                exposure.adaptationMode.Override(AdaptationMode.Fixed);
                var volume = light.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 2000f;
                volume.sharedProfile = profile;

                made.SetActive(true);
                for (int i = 0; i < Renders; i++)
                {
                    camera.Render();
                }
                RenderTexture.active = target;
                shot = new Texture2D(Wide, High, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, Wide, High), 0, 0, false);
                shot.Apply(false, true);   // no longer readable: the copy in main memory is let go
                shot.name = "RealLifeOverhaul Showroom photo";
            }
            catch (Exception e)
            {
                DealerApp.Logger?.Warn("Car Showroom: a photo of a car could not be taken (" + e.Message + "); the game's own picture of a vehicle is shown.");
                if (shot != null)
                {
                    UnityEngine.Object.Destroy(shot);
                }
                shot = null;
            }
            finally
            {
                RenderTexture.active = before;
                if (target != null)
                {
                    RenderTexture.ReleaseTemporary(target);
                }
                made.SetActive(false);
                UnityEngine.Object.Destroy(made);
                if (light != null)
                {
                    light.SetActive(false);   // out of the game's volumes now, before its own camera draws the frame
                    UnityEngine.Object.Destroy(light);
                }
                if (profile != null)
                {
                    UnityEngine.Object.Destroy(profile);
                }
            }
            return shot;
        }
    }
}
