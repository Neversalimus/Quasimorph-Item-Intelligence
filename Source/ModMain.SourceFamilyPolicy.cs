namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static string[] GetRandomStartingPoolKeys(bool current104)
        {
            // GenerateStartingItems reads bucket 10 in both audited versions.
            // The 1.0.4 pools are distinct: never substitute legacy reward contents
            // when current-game data is absent. Callers must pass the source-family gate.
            return current104
                ? new string[] { "RandomStart_rewardEquipment", "RandomStart_rewardConsumables" }
                : new string[] { "General_rewardEquipment", "General_rewardConsumables" };
        }
    }
}
