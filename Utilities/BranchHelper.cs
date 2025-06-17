using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Utilities
{
    public static class BranchHelper
    {
        public static string GetBranchUrl(string rawBranchName)
        {
            if (string.IsNullOrWhiteSpace(rawBranchName)) return "";

            string branchName = CleanBranchName(rawBranchName);

            if (branchName == "Baildon & Guiseley")
            {
                return "https://dacres.co.uk/estate-agents/baildon/";
            }

            string slug = GenerateSlug(branchName);
            return $"https://dacres.co.uk/estate-agents/{slug}/";
        }

        private static string CleanBranchName(string raw)
        {
            string decoded = WebUtility.HtmlDecode(raw).Trim();

            if (decoded.StartsWith("Dacres ", StringComparison.OrdinalIgnoreCase))
                decoded = decoded.Substring(7).Trim();

            return decoded;
        }

        private static string GenerateSlug(string input)
        {
            input = input.ToLowerInvariant();
            input = Regex.Replace(input, @"[&+]", " ");
            input = Regex.Replace(input, @"[^a-z0-9\s-]", "");
            input = Regex.Replace(input, @"[\s-]+", "-").Trim('-');
            return input;
        }
    }
}
