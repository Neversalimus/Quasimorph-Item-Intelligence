using System;
using System.Collections.Generic;
using System.Globalization;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Player-facing Overview must describe what the player can act on now, not the
        // total number of historical/future Magnum relations. The full Magnum tab remains
        // authoritative for completed and locked research.
        private sealed class OverviewMagnumState
        {
            public bool HasRelations;
            public int CurrentRequired;
            public int FutureRequired;
            public int UnknownRequired;
        }

        private static OverviewMagnumState GetOverviewMagnumState(string itemId)
        {
            OverviewMagnumState state = new OverviewMagnumState();
            if (!ShowMagnumUses || string.IsNullOrEmpty(itemId)) return state;

            TryResolveMagnumProgressionLightweight();
            List<MagnumUse> uses;
            if (!MagnumUses.TryGetValue(itemId, out uses) || uses == null || uses.Count == 0)
                return state;

            state.HasRelations = true;
            MagnumSnapshot snapshot = GetMagnumSnapshot(itemId);
            if (snapshot == null) return state;

            // Same snapshot as the detailed Magnum state: one semantics owner.
            state.CurrentRequired = snapshot.CurrentRequired;
            state.FutureRequired = snapshot.FutureRequired;
            state.UnknownRequired = snapshot.UnknownRequired;
            return state;
        }

        private static string FormatOverviewMagnumStatus(OverviewMagnumState state)
        {
            if (state == null || !state.HasRelations) return string.Empty;
            if (state.CurrentRequired > 0)
                return state.CurrentRequired.ToString(CultureInfo.InvariantCulture) + " " + Ui("ui.overview_magnum_available_now");
            if (state.UnknownRequired > 0)
                return Ui("ui.state_unknown");
            if (state.FutureRequired > 0)
                return Ui("ui.overview_magnum_not_available_now");
            return Ui("ui.overview_magnum_all_completed");
        }
    }
}
