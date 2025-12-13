using BepInEx.Logging;

namespace AlgoritmaPuncakMod
{
    internal static class ModLogger
    {
        private static ManualLogSource Source => AlgoritmaPuncakMod.Log;

        internal static void Debug(string message, bool force = false)
        {
            if (!force && !AlgoritmaPuncakMod.DebugInstrumentation)
            {
                return;
            }

            Source?.LogDebug(message);
        }

        internal static void Info(string message, bool force = false)
        {
            if (!force && !AlgoritmaPuncakMod.DebugInstrumentation)
            {
                return;
            }

            Source?.LogInfo(message);
        }

        internal static void Warn(string message, bool force = false)
        {
            if (!force && !AlgoritmaPuncakMod.DebugInstrumentation)
            {
                return;
            }

            Source?.LogWarning(message);
        }

        internal static void Error(string message)
        {
            Source?.LogError(message);
        }
    }
}
