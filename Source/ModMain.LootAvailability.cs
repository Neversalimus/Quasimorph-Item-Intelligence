namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // The suggested action must have the same availability as the actual control.
        private static string GetLootModifierUnavailableNoteKey(bool manualAvailable)
        {
            return manualAvailable ? "loot.note.container_modifier_unavailable"
                : "loot.note.container_modifier_basic";
        }

        private static string GetLootManualUnavailableNoteKey(bool supportedAssembly)
        {
            return supportedAssembly ? "loot.note.manual_unavailable"
                : "loot.note.manual_compatibility";
        }

        private static string GetLootContainerUnavailableNoteKey(bool calculationAvailable, bool supportedAssembly)
        {
            if (calculationAvailable) return "loot.note.container_save_unavailable";
            return supportedAssembly ? "loot.note.container_calculation_unavailable"
                : "loot.note.container_save_compatibility";
        }
    }
}
