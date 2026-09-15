using UnityEngine;

namespace ItemIntelligence
{
    /// <summary>
    /// Centralized optional diagnostic logging.
    /// Normal mode keeps Player.log concise while warnings/errors remain visible.
    /// </summary>
    public static partial class ModMain
    {
        private static void VerboseLog(string message)
        {
            if (!VerboseLogging)
                return;

            Debug.Log(message);
        }
    }
}
