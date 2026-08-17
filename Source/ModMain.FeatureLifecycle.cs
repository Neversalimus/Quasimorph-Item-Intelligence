namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // v1.7.36-test9: the persistent driver invokes one explicit lifecycle boundary.
        // Each feature owns its compatibility boundary, frame budget, UI refresh and
        // shutdown cancellation. This intentionally stays a small static coordinator;
        // no dynamic module registry or dependency-injection layer is introduced.
        private static void StartFeatureWarmupsAfterCoreIndexes()
        {
            StartAmmoFeatureWarmup();
            StartDisassemblyFeatureWarmup();
            StartFactionFeatureWarmup();
            StartLootFeatureWarmup();
        }

        private static void TickFeatureFrameWork()
        {
            // Preserve the established order of the retained feature warmups.
            TickAmmoFeatureFrameWork();
            TickDisassemblyFeatureFrameWork();
            TickFactionFeatureFrameWork();
            TickLootFeatureFrameWork();
        }

        private static void StopFeatureFrameWorkForApplicationQuit()
        {
            StopAmmoFeatureFrameWork();
            StopDisassemblyFeatureFrameWork();
            StopFactionFeatureFrameWork();
            StopLootFeatureFrameWork();
            StopRuntimeServiceFrameWork();
        }

        private static string DescribeFeatureWarmupStates()
        {
            return "AmmoWarmup=" + GetAmmoWarmupStatus() +
                ", DisassemblyWarmup=" + GetDisassemblyWarmupStatus() +
                ", FactionTech=" + GetFactionWarmupStatus() +
                ", Loot=" + GetLootWarmupStatus();
        }
    }
}
