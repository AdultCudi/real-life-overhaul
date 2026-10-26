using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Entities;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Sits on the game's row of category buttons (ContactCategorySelection). One frame after the row is built again (the app
    /// opens, a contact is added or taken away, the option changes) it dresses the buttons: a second line in each button's
    /// tooltip, how many contacts the category holds; the mod's own buttons in a dark picture the game's buttons have; and the
    /// whole row made smaller when more buttons stand in it than its width holds (the game's seven and the mod's seven are
    /// fourteen). Event-driven: there is no Update, so it costs nothing a frame.
    /// </summary>
    public sealed class ContactGroupsRow : MonoBehaviour
    {
        private static readonly FeatureGuard Fault = Guard.For("phone", "ContactGroupsRow", typeof(ContactGroupsRow));
        private static readonly Color Dark = new Color(0.13f, 0.15f, 0.2f, 1f);   // a picture on the buttons' white, when the game's own has no tint to copy

        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        private bool _asked;
        private bool _measured;
        private float _wide;            // the row's width as the game made it
        private Vector3 _scale;         // and its scale

        /// <summary>Dresses the buttons one frame from now, once however many times it is asked.</summary>
        public void Request()
        {
            if (_asked || !isActiveAndEnabled || Fault.Off)
            {
                return;
            }
            _asked = true;
            StartCoroutine(Guard.Routine(Later()));
        }

        private void OnEnable()
        {
            Request();
        }

        private IEnumerator Later()
        {
            yield return null;   // after the game has built its buttons, whichever listener ran first
            _asked = false;
            Dress();
        }

        private void Dress()
        {
            var rect = (RectTransform)transform;
            if (!_measured)
            {
                _measured = true;
                _wide = rect.sizeDelta.x;
                _scale = rect.localScale;
            }
            bool on = ContactGroupsGame.On;
            ContactCategoryButton[] buttons = GetComponentsInChildren<ContactCategoryButton>(false);
            List<Contact> contacts = SaveGameManager.Current != null ? SaveGameManager.Current.Contacts : null;
            FieldInfo iconField = GameContract.ContactButtonIcon.Field;
            Color tint = Dark;
            for (int i = 0; on && i < buttons.Length; i++)
            {
                Image image = ContactGroups.GroupOfCategory((int)buttons[i].categoryName) == ContactGroup.None ? IconOf(iconField, buttons[i]) : null;
                if (image != null && image.color.r * 0.2126f + image.color.g * 0.7152f + image.color.b * 0.0722f < 0.6f)
                {
                    tint = image.color;   // the game's own buttons' picture colour, when it is a dark one
                    break;
                }
            }
            System.Text.StringBuilder counts = Meta.Dev.Verbose ? new System.Text.StringBuilder(96) : null;
            for (int i = 0; i < buttons.Length; i++)
            {
                ContactCategoryButton button = buttons[i];
                BasicTooltip tip = button.GetComponent<BasicTooltip>();
                int count = Count(contacts, button.categoryName);
                if (tip != null)
                {
                    if (on)
                    {
                        tip.descriptionKey = ContactGroups.CountKey;
                        tip.localizationArguments = new { count };
                    }
                    else if (tip.descriptionKey == ContactGroups.CountKey)
                    {
                        tip.descriptionKey = null;
                        tip.localizationArguments = null;
                    }
                }
                if (counts != null)
                {
                    counts.Append(counts.Length > 0 ? ", " : string.Empty).Append((int)button.categoryName).Append('=').Append(count);
                }
                Image icon = on && ContactGroups.GroupOfCategory((int)button.categoryName) != ContactGroup.None ? IconOf(iconField, button) : null;
                if (icon != null)
                {
                    icon.color = tint;
                }
            }
            Fit(rect, buttons, on, counts);
        }

        // The row as wide as the game made it: shrunk, with its width made larger by the same amount, when its buttons ask for more.
        private void Fit(RectTransform rect, ContactCategoryButton[] buttons, bool on, System.Text.StringBuilder counts)
        {
            HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
            float need = 0f, scale = 1f;
            if (layout != null && buttons.Length > 0 && rect.anchorMin.x == rect.anchorMax.x)
            {
                float each = ((RectTransform)buttons[0].transform).rect.width;
                need = ContactGroups.RowNeeds(buttons.Length, each, layout.spacing, layout.padding.left + layout.padding.right);
                scale = on ? ContactGroups.ScaleFor(need, _wide) : 1f;
                rect.sizeDelta = new Vector2(scale < 1f ? need : _wide, rect.sizeDelta.y);
                rect.localScale = scale < 1f ? new Vector3(_scale.x * scale, _scale.y * scale, _scale.z) : _scale;
            }
            if (counts != null)
            {
                ContactGroupsGame.Logger?.Info("Contacts check (groups, row): " + buttons.Length + " button(s) [" + counts + "], the row asks "
                              + need.ToString("0", Inv) + " of " + _wide.ToString("0", Inv)
                              + ", scale " + scale.ToString("0.00", Inv) + ": " + (scale >= 0.5f ? "PASS" : "FAIL"));
            }
        }

        private static Image IconOf(FieldInfo field, ContactCategoryButton button)
        {
            return field != null ? field.GetValue(button) as Image : null;
        }

        private static int Count(List<Contact> contacts, ContactCategoryName category)
        {
            int n = 0;
            for (int i = 0; contacts != null && i < contacts.Count; i++)
            {
                if (contacts[i] != null && contacts[i].category == category)
                {
                    n++;
                }
            }
            return n;
        }
    }
}
