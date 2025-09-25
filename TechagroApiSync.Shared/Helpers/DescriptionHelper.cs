using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TechagroApiSync.Shared.Helpers
{
    public static class DescriptionHelper
    {
        public static string TruncateHtml(string html, int maxLength)
        {
            if (string.IsNullOrEmpty(html) || html.Length <= maxLength)
                return html;

            // Find the last safe closing tag before the limit
            int lastLiClose = html.LastIndexOf("</li>", maxLength, StringComparison.OrdinalIgnoreCase);
            int lastPClose = html.LastIndexOf("</p>", maxLength, StringComparison.OrdinalIgnoreCase);
            int lastDivClose = html.LastIndexOf("</div>", maxLength, StringComparison.OrdinalIgnoreCase);

            // Choose the last valid cutoff point
            int cutoff = Math.Max(lastLiClose, Math.Max(lastPClose, lastDivClose));
            if (cutoff == -1) cutoff = maxLength;

            // Find actual closing tag length
            string tag = null;
            if (cutoff == lastLiClose) tag = "</li>";
            else if (cutoff == lastPClose) tag = "</p>";
            else if (cutoff == lastDivClose) tag = "</div>";

            int cutoffLength = tag?.Length ?? 0;
            if (cutoff + cutoffLength > html.Length)
                cutoffLength = 0;

            string truncated = html.Substring(0, Math.Min(cutoff + cutoffLength, html.Length));

            // Ensure all opened tags are closed properly
            var stack = new Stack<string>();
            var regex = new Regex(@"</?([a-zA-Z0-9]+)[^>]*>");
            foreach (Match match in regex.Matches(truncated))
            {
                if (!match.Value.StartsWith("</"))
                    stack.Push(match.Groups[1].Value);
                else if (stack.Count > 0 && stack.Peek().Equals(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                    stack.Pop();
            }

            // Close any still-open tags, but keep length <= maxLength
            while (stack.Count > 0)
            {
                string closeTag = $"</{stack.Pop()}>";
                if (truncated.Length + closeTag.Length > maxLength)
                    break; // stop if adding would exceed limit
                truncated += closeTag;
            }

            // Final safety: hard cut if still too long
            if (truncated.Length > maxLength)
                truncated = truncated.Substring(0, maxLength);

            return truncated;
        }
    }
}