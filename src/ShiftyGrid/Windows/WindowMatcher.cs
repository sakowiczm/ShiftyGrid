using ShiftyGrid.Configuration;

namespace ShiftyGrid.Windows
{
    internal class WindowMatcher
    {
        /// <summary>
        /// Finds the first matching rule for a window using strict AND logic.
        /// All defined conditions (TitlePattern, ClassName, ProcessName) must be fulfilled for a match.
        /// All comparisons are case-insensitive.
        /// </summary>
        public static WindowMatch? FindMatchingRule(Window window, OrganizeConfig config)
        {
            // Get process name (may be null for elevated windows)
            var processName = window.GetProcessName();

            foreach (var matcher in config.Matchers)
            {
                bool allConditionsMet = true;

                // Check TitlePattern (if defined)
                if (!string.IsNullOrEmpty(matcher.TitlePattern))
                {
                    if (string.IsNullOrEmpty(window.Text) ||
                        !window.Text.Contains(matcher.TitlePattern, StringComparison.OrdinalIgnoreCase))
                    {
                        allConditionsMet = false;
                    }
                }

                // Check ClassName (if defined)
                if (allConditionsMet && !string.IsNullOrEmpty(matcher.ClassName))
                {
                    if (string.IsNullOrEmpty(window.ClassName) ||
                        !window.ClassName.Equals(matcher.ClassName, StringComparison.OrdinalIgnoreCase))
                    {
                        allConditionsMet = false;
                    }
                }

                // Check ProcessName (if defined)
                if (allConditionsMet && !string.IsNullOrEmpty(matcher.ProcessName))
                {
                    if (processName == null ||
                        !processName.Equals(matcher.ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        allConditionsMet = false;
                    }
                }

                // Return first matcher where all conditions are met
                if (allConditionsMet)
                {
                    return matcher;
                }
            }

            return null; // No matcher matched all conditions
        }

    }
}
