using System;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>The pager stepping aside for another mod that pages the same grid.</summary>
    public sealed partial class PhoneGridPager
    {
        private bool SomeoneElse()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.gameObject.activeSelf && PagedOff(child))
                {
                    return true;   // a button paged off has been switched on by another hand
                }
            }
            _scripts.Clear();
            GetComponents(_scripts);
            for (int i = 0; i < _scripts.Count; i++)
            {
                MonoBehaviour script = _scripts[i];
                if (script == null || script == this)
                {
                    continue;
                }
                if (!Known(script.GetType()))
                {
                    return true;
                }
            }
            return false;
        }

        // Worked out once per script type (the assembly's name is made each time it is asked).
        private static bool Known(Type type)
        {
            if (KnownScripts.TryGetValue(type, out bool known))
            {
                return known;
            }
            string assembly = type.Assembly.GetName().Name;
            known = type.Assembly == typeof(SmartphoneUI).Assembly || assembly.StartsWith("Unity", StringComparison.Ordinal)
                    || assembly.StartsWith("BigAmbitions", StringComparison.Ordinal) || assembly == "HGPlugins" || assembly == "HGExtensions"
                    || assembly == "ExternalPlugins" || type.Assembly == typeof(PhoneGridPager).Assembly;
            KnownScripts[type] = known;
            return known;
        }

        private void StepAside()
        {
            _aside = true;
            ShowAll();
            RestoreRow();
            if (_more != null)
            {
                Destroy(_more.gameObject);
                _more = null;
            }
            DropFolders();
            UiClone.Fallback(_strip ? "another mod pages the full menu's app buttons, so this one leaves the pages to it"
                                    : "another mod pages the phone's home screen, so this one leaves the pages to it");
        }

        private void ShowAll()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                var mark = child.GetComponent<PageMark>();
                if (mark == null || !mark.Off)
                {
                    continue;
                }
                mark.Off = false;
                child.gameObject.SetActive(true);
            }
        }

        // Stepping aside, or the pager gone.
        private void DropFolders()
        {
            _open = -1;
            _foldered = false;
            if (_folders != null)
            {
                _folders.Destroy();
                _folders = null;
            }
            if (_keys != null)
            {
                _keys.enabled = false;
            }
        }
    }
}
