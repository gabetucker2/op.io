using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace op.io
{
    public class TriggerManager
    {
        private static Dictionary<string, bool> triggers = []; // If a trigger key is true, then it means there was a change this tick

        public static void PrimeTrigger(string key)
        {
            triggers[key] = true;
        }

        public static void PrimeTriggerIfTrue(string key, bool val)
        {
            if (val)
            {
                PrimeTrigger(key);
            }
        }

        public static bool GetTrigger(string key)
        {
            if (triggers.ContainsKey(key))
            {
                return triggers[key];
            }
            return false;
        }

        public static void Tickwise_TriggerReset()
        {
            foreach (string key in triggers.Keys)
            {
                triggers[key] = false;
            }
        }
    }
}
