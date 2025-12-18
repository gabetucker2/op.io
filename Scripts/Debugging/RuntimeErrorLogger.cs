using System;
using System.Text;
using System.Threading.Tasks;

namespace op.io
{
    public static class RuntimeErrorLogger
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            _initialized = true;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception exception = args.ExceptionObject as Exception ??
                new Exception($"Unhandled exception object: {args.ExceptionObject}");

            LogException("UnhandledException", exception, args.IsTerminating);
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args)
        {
            LogException("UnobservedTaskException", args.Exception, isTerminating: false);
            args.SetObserved();
        }

        private static void LogException(string source, Exception exception, bool isTerminating)
        {
            string payload = BuildPayload(source, exception, isTerminating);

            try
            {
                LogFileHandler.AppendLog(payload);
            }
            catch
            {
                // Do not let logging failures hide the original crash.
            }

            try
            {
                Console.Error.WriteLine(payload);
            }
            catch
            {
                // If console output is unavailable, we still attempted to persist the error to disk.
            }
        }

        private static string BuildPayload(string source, Exception exception, bool isTerminating)
        {
            StringBuilder builder = new();
            builder.Append($"[{DateTime.Now:O}] [{source}] {(isTerminating ? "Terminating" : "Continuing")} ");

            if (exception == null)
            {
                builder.Append("No exception information available.");
                return builder.ToString();
            }

            builder.AppendLine(exception.ToString());
            return builder.ToString().TrimEnd();
        }
    }
}
