using System;
using System.Collections.Generic;
using System.Linq;

namespace MessageParser.Core.Rules
{
    public interface IRuleEngine
    {
        IReadOnlyList<IRule> Rules { get; }
        void RegisterRule(IRule rule);
        bool SetRuleEnabled(string ruleId, bool enabled);
        bool SetRuleParameter(string ruleId, string parameterKey, object value);
    }

    public class RuleEngine<TContext> : IRuleEngine where TContext : RuleContext
    {
        private readonly List<IRule<TContext>> _rules = new List<IRule<TContext>>();
        private readonly object _lock = new object();

        public IReadOnlyList<IRule> Rules
        {
            get
            {
                lock (_lock)
                {
                    return _rules.Cast<IRule>().ToList();
                }
            }
        }

        public void RegisterRule(IRule<TContext> rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            lock (_lock)
            {
                if (!_rules.Any(r => r.Id.Equals(rule.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    _rules.Add(rule);
                }
            }
        }

        public void RegisterRule(IRule rule)
        {
            if (rule is IRule<TContext> typedRule)
            {
                RegisterRule(typedRule);
            }
            else
            {
                throw new ArgumentException($"Rule '{rule?.Name ?? "Unknown"}' ({rule?.GetType().Name}) is not compatible with context type {typeof(TContext).Name}.", nameof(rule));
            }
        }

        public bool SetRuleEnabled(string ruleId, bool enabled)
        {
            lock (_lock)
            {
                var rule = _rules.FirstOrDefault(r => r.Id.Equals(ruleId, StringComparison.OrdinalIgnoreCase));
                if (rule != null)
                {
                    rule.IsEnabled = enabled;
                    return true;
                }
                return false;
            }
        }

        public bool SetRuleParameter(string ruleId, string parameterKey, object value)
        {
            lock (_lock)
            {
                var rule = _rules.FirstOrDefault(r => r.Id.Equals(ruleId, StringComparison.OrdinalIgnoreCase));
                if (rule != null)
                {
                    rule.SetParameter(parameterKey, value);
                    return true;
                }
                return false;
            }
        }

        public List<RuleExecutionResult> EvaluateAll(TContext context)
        {
            List<IRule<TContext>> rulesToEvaluate;
            lock (_lock)
            {
                rulesToEvaluate = _rules.Where(r => r.IsEnabled).ToList();
            }

            var results = new List<RuleExecutionResult>();

            foreach (var rule in rulesToEvaluate)
            {
                var result = rule.Evaluate(context);
                if (result.Triggered)
                {
                    results.Add(result);
                }
            }

            return results;
        }
    }

    public class RuleEngine : RuleEngine<RuleContext>
    {
    }
}
