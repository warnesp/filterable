using System;
using System.Text.RegularExpressions;

namespace MessageParser.Core.Filtering
{
    public class FilterQuery
    {
        public string PropertyName { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string RawQuery { get; set; } = string.Empty;
    }

    public static class QueryParser
    {
        // Matches <PropertyName> <Operator> <Value>
        // Operators can be: >=, <=, >, <, ==, !=, =, contains, startswith, endswith
        private static readonly Regex QueryRegex = new Regex(
            @"^\s*(?<prop>[a-zA-Z_][a-zA-Z0-9_]*)\s*(?<op>>=|<=|>|<|==|!=|=|\bcontains\b|\bstartswith\b|\bendswith\b)\s*(?<val>.+)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static FilterQuery Parse(string queryText)
        {
            if (string.IsNullOrWhiteSpace(queryText))
            {
                return new FilterQuery { IsValid = false, RawQuery = queryText };
            }

            var match = QueryRegex.Match(queryText);
            if (match.Success)
            {
                return new FilterQuery
                {
                    PropertyName = match.Groups["prop"].Value.Trim(),
                    Operator = match.Groups["op"].Value.Trim(),
                    Value = match.Groups["val"].Value.Trim(),
                    IsValid = true,
                    RawQuery = queryText
                };
            }

            return new FilterQuery { IsValid = false, RawQuery = queryText };
        }
    }
}
