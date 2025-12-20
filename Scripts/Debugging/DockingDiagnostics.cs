using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace op.io
{
    /// <summary>
    /// Centralized diagnostics for tracking why DockingMode ends up enabled by default.
    /// Writes to the normal debug logger (no dedicated dock debug file anymore).
    /// </summary>
    internal static class DockingDiagnostics
    {
        private const string DockingSettingKey = "DockingMode";
        private static readonly object Sync = new();
        private static bool _initialized;

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (Sync)
            {
                if (_initialized)
                {
                    return;
                }

                _initialized = true;
            }
        }

        private static void AppendRaw(string message)
        {
            EnsureInitialized();
            DebugLogger.PrintDebug(message);
        }

        private static void Append(string category, string message, string source, string context, string stackTrace)
        {
            string prefix = $"{DateTime.Now:O} [{category}]";
            string composed = $"{prefix} {message} | source={source}{(string.IsNullOrWhiteSpace(context) ? string.Empty : $" | context={context}")}{(string.IsNullOrWhiteSpace(stackTrace) ? string.Empty : $" | stack={stackTrace}")}";
            AppendRaw(composed);
        }

        private static string DescribeCaller(string callerFilePath, string callerMember, int callerLine)
        {
            string file = string.IsNullOrWhiteSpace(callerFilePath) ? "unknown" : Path.GetFileName(callerFilePath);
            return $"{file}::{callerMember}@{callerLine}";
        }

        private static string CaptureStack(int skipFrames = 2, int depth = 4)
        {
            try
            {
                StackTrace trace = new(skipFrames, true);
                IEnumerable<string> frames = trace.GetFrames()?
                    .Take(depth)
                    .Select(f => $"{Path.GetFileName(f.GetFileName())}::{f.GetMethod()?.Name}@{f.GetFileLineNumber()}") ?? Enumerable.Empty<string>();
                string joined = string.Join(" <= ", frames.Where(f => !string.IsNullOrWhiteSpace(f)));
                return joined;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string FormatBool(bool value) => value ? "ON" : "OFF";
        private static string FormatNullable<T>(T value) => value == null ? "null" : value.ToString();

        public static void RecordMigration(string stage, int rawState, bool boolState, bool markerOffExists, bool markerOnExists, string note = null, string context = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMember = "", [CallerLineNumber] int callerLine = 0)
        {
            string caller = DescribeCaller(callerFilePath, callerMember, callerLine);
            string stack = CaptureStack();
            Append("MIGRATION", $"stage={stage} rawState={rawState} boolState={FormatBool(boolState)} markerOffExists={markerOffExists} markerOnExists={markerOnExists} note={note}", caller, context, stack);
        }

        public static void RecordRawControlState(string source, string settingKey, string inputKey, string inputType, int rawState, bool interpretedState, bool saveToBackend, string note = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMember = "", [CallerLineNumber] int callerLine = 0)
        {
            if (!string.Equals(settingKey, DockingSettingKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string caller = DescribeCaller(callerFilePath, callerMember, callerLine);
            string stack = CaptureStack();
            Append("LOAD", $"source={source} inputKey='{inputKey}' inputType={inputType} rawState={rawState} boolState={FormatBool(interpretedState)} saveToBackend={saveToBackend} note={note}", caller, null, stack);
        }

        public static void RecordSwitchInitialization(string source, bool dbState, int dbRaw, bool requestedState, bool hasPersistenceFlag, string context = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMember = "", [CallerLineNumber] int callerLine = 0)
        {
            string caller = DescribeCaller(callerFilePath, callerMember, callerLine);
            string stack = CaptureStack();
            Append("INIT", $"source={source} dbRaw={dbRaw} dbBool={FormatBool(dbState)} requested={FormatBool(requestedState)} hasPersistenceFlag={hasPersistenceFlag}", caller, context, stack);
        }

        public static void RecordSwitchChange(string source, bool previous, bool next, bool persisted, string context = null, string reason = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMember = "", [CallerLineNumber] int callerLine = 0)
        {
            string caller = DescribeCaller(callerFilePath, callerMember, callerLine);
            string stack = CaptureStack();
            Append("SET", $"source={source} prev={FormatBool(previous)} next={FormatBool(next)} persisted={persisted} reason={reason}", caller, context, stack);
        }

        public static void RecordNoOpSet(string source, bool state, string context = null, string reason = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMember = "", [CallerLineNumber] int callerLine = 0)
        {
            string caller = DescribeCaller(callerFilePath, callerMember, callerLine);
            string stack = CaptureStack();
            Append("SET-NOOP", $"source={source} state={FormatBool(state)} reason={reason}", caller, context, stack);
        }

        public static void RecordInputEdge(string source, string inputKey, bool newState, string context = null, string note = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMember = "", [CallerLineNumber] int callerLine = 0)
        {
            string caller = DescribeCaller(callerFilePath, callerMember, callerLine);
            string stack = CaptureStack();
            Append("INPUT", $"source={source} inputKey='{inputKey}' newState={FormatBool(newState)} note={note}", caller, context, stack);
        }

        public static void RecordConsumerUpdate(string source, bool state, string context = null, string note = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMember = "", [CallerLineNumber] int callerLine = 0)
        {
            string caller = DescribeCaller(callerFilePath, callerMember, callerLine);
            string stack = CaptureStack();
            Append("CONSUMER", $"source={source} docking={FormatBool(state)} note={note}", caller, context, stack);
        }

        public static void RecordBlockToggle(string source, bool state, string context = null, string note = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMember = "", [CallerLineNumber] int callerLine = 0)
        {
            string caller = DescribeCaller(callerFilePath, callerMember, callerLine);
            string stack = CaptureStack();
            Append("BLOCK", $"source={source} dockingMode={FormatBool(state)} note={note}", caller, context, stack);
        }
    }
}
