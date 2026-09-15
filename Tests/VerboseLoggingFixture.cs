// Test-only fixture for isolated production fragments that call ModMain.VerboseLog.
// Full mod builds use Source/ModMain.Logging.cs. Isolated suites default to silent
// logging, but can opt into a test sink when they explicitly verify verbose diagnostics.
namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static bool _testVerboseLoggingEnabled = false;
        private static System.Action<string> _testVerboseLogSink = null;

        private static void VerboseLog(string message)
        {
            if (!_testVerboseLoggingEnabled || _testVerboseLogSink == null)
                return;

            _testVerboseLogSink(message);
        }
    }
}
