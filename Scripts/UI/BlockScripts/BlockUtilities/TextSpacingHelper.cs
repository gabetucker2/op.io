using System.Collections.Generic;

namespace op.io.UI.BlockScripts.BlockUtilities
{
    internal static class TextSpacingHelper
    {
        public const string WideWordSeparator = "    ";

        public static string JoinWithWideSpacing(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            List<string> filtered = new(parts.Length);
            foreach (string part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                {
                    filtered.Add(trimmed);
                }
            }

            return filtered.Count == 0 ? string.Empty : string.Join(WideWordSeparator, filtered);
        }
    }
}
