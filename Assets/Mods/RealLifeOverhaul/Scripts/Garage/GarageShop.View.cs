using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Cinemachine;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>The turntable part of <see cref="GarageShop"/>: the camera round the car and the plate it is dragged on.</summary>
    public sealed partial class GarageShop
    {
        private void View(GameManager gm)
        {
            if (_vcam == null)
            {
                var go = new GameObject("RealLifeOverhaul Garage Camera");
                go.transform.SetParent(_owner, false);
                _vcam = go.AddComponent<CinemachineVirtualCamera>();
                if (gm != null && gm.vehicleCamera is CinemachineVirtualCamera vanilla)
                {
                    _vcam.m_Lens = vanilla.m_Lens;   // keep the game's clip planes
                }
                LensSettings lens = _vcam.m_Lens;
                lens.FieldOfView = 40f;
                _vcam.m_Lens = lens;
            }
            float length = 4.6f;
            float height = 1.5f;
            if (GarageFit.TryGet(_car.vehicleType.vehicleTypeName, out CarFit fit))
            {
                length = fit.Front - fit.Rear;
                height = fit.Top;
            }
            else if (_car.vehicleCollider != null)
            {
                Bounds bounds = _car.vehicleCollider.bounds;
                length = Mathf.Max(bounds.size.x, bounds.size.z);
                height = bounds.size.y;
            }
            _focus = new Vector3(0f, height * 0.45f, 0f);
            _minDistance = length * 0.9f;
            _maxDistance = length * 3f;
            _distance = length * 1.55f;
            _yaw = _car.transform.eulerAngles.y + 145f;   // three-quarter view from the front
            _pitch = 14f;
            _vcam.gameObject.SetActive(true);
            _vcam.Priority = CameraPriority;
            Aim();
        }

        private void Aim()
        {
            if (_vcam == null || _car == null)
            {
                return;
            }
            Vector3 target = _car.transform.TransformPoint(_focus);
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            // A little left of its line to the car, so the car is seen between the shop's list and the game's phone.
            _vcam.transform.SetPositionAndRotation(target - rotation * Vector3.forward * _distance - rotation * Vector3.right * (_distance * ViewAside), rotation);
        }

        private void Turn(Vector2 pixels)
        {
            _yaw += pixels.x * 0.25f;
            _pitch = Mathf.Clamp(_pitch - pixels.y * 0.2f, 3f, 65f);
            Aim();
        }

        private void Zoom(float notches)
        {
            _distance = Mathf.Clamp(_distance - notches * 0.5f, _minDistance, _maxDistance);
            Aim();
        }

        // An invisible plate behind everything on the HUD: dragging turns the view, the wheel moves it in and out, and no
        // click in the shop reaches the world behind.
        private void DragArea()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform canvas = uis != null && uis.playerHUD != null ? uis.playerHUD.transform : null;
            if (canvas == null || _dragArea != null)
            {
                return;
            }
            var made = new GameObject("RealLifeOverhaul Shop View", typeof(RectTransform));
            made.transform.SetParent(canvas, false);
            var rect = (RectTransform)made.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            made.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var area = made.AddComponent<GarageDragArea>();
            area.Dragged = Turn;
            area.Scrolled = Zoom;
            _dragArea = made;
        }
    }
}
