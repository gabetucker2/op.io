using System;
using System.Collections.Generic;

namespace op.io
{
    public static class SwitchRegistry
    {
        private static readonly Dictionary<string, List<Action<bool>>> _consumers = new(StringComparer.OrdinalIgnoreCase);

        public static void RegisterConsumer(string settingKey, Action<bool> consumer)
        {
            if (string.IsNullOrWhiteSpace(settingKey) || consumer == null)
            {
                return;
            }

            if (!_consumers.TryGetValue(settingKey, out var list))
            {
                list = [];
                _consumers[settingKey] = list;
            }

            list.Add(consumer);
        }

        public static void ClearConsumers()
        {
            _consumers.Clear();
        }

        public static void NotifyConsumers(string settingKey, bool state)
        {
            if (!_consumers.TryGetValue(settingKey, out var list))
            {
                return;
            }

            foreach (var consumer in list)
            {
                try
                {
                    consumer(state);
                }
                catch (Exception ex)
                {
                    DebugLogger.PrintError($"Switch consumer for '{settingKey}' threw an exception: {ex.Message}");
                }
            }
        }
    }
}
