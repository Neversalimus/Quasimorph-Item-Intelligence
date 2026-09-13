using System;
using System.Collections;
using MGSC;
using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static bool IsTradeTravelSpaceContext()
        {
            // Read the live UI owner on every check. Never retain a positive result
            // across a space/mission transition, or scan all loaded Unity objects for
            // each station. UI.Get<T>() is also avoided: it logs when a view is absent.
            MGSC.UI ui = SingletonMonoBehaviour<MGSC.UI>.Instance;
            if (ui == null) return false;
            Type hudType = typeof(SpaceHudScreen);
            if (MGSC.UI.DefaultView == hudType) return true;

            // Preserve the active-HUD fallback using the UI-owned registry. Lookup
            // does not instantiate a view or change the currently displayed screen.
            IDictionary views = GetMember(ui, "_instantiatedViews") as IDictionary;
            Component hud = views == null ? null : views[hudType] as Component;
            return hud != null && hud.gameObject != null && hud.gameObject.activeInHierarchy;
        }
    }
}
