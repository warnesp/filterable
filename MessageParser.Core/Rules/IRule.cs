using System.Collections.Generic;

namespace MessageParser.Core.Rules
{
    public interface IRule
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        string Category { get; }
        bool IsEnabled { get; set; }

        /// <summary>
        /// Key-value parameters for this rule (e.g. timeout values, thresholds).
        /// </summary>
        IReadOnlyDictionary<string, object> Parameters { get; }

        /// <summary>
        /// Update a rule parameter value.
        /// </summary>
        void SetParameter(string key, object value);
    }

    /// <summary>
    /// Strongly-typed rule evaluating a specific RuleContext type.
    /// Contravariant ('in TContext') allows rules for base contexts to evaluate derived contexts.
    /// </summary>
    public interface IRule<in TContext> : IRule where TContext : RuleContext
    {
        /// <summary>
        /// Evaluates the rule against the current context.
        /// </summary>
        RuleExecutionResult Evaluate(TContext context);
    }
}
