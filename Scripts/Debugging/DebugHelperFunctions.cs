using Microsoft.Xna.Framework;
using System.Runtime.CompilerServices;
using System.IO;

namespace op.io
{
    public static class DebugHelperFunctions
    {
        public static void DeltaTimeZeroWarning(
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMethod = "",
            [CallerLineNumber] int callerLine = 0)
        {
            if (Core.DELTATIME <= 0f && Core.GAMETIME != 0f)
            {
                string sourceTrace = GenerateSourceTrace(callerFilePath, callerMethod, callerLine);
                DebugLogger.PrintWarning($"DeltaTime is {Core.DELTATIME}. This may cause unexpected behavior.", 4);
            }
        }

        public static string GenerateSourceTrace(
            string callerFilePath,
            string callerMethod,
            int callerLine)
        {
            string callerFileName = Path.GetFileName(callerFilePath);
            return $"{callerFileName}::{callerMethod} @ Line {callerLine}";
        }
    }
}
