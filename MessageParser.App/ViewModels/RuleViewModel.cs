using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MessageParser.Core.Rules;

namespace MessageParser.App.ViewModels
{
    public partial class RuleViewModel : ObservableObject
    {
        private readonly IRule _rule;

        public string Id => _rule.Id;
        public string Name => _rule.Name;
        public string Description => _rule.Description;
        public string Category => _rule.Category;

        [ObservableProperty]
        private bool _isEnabled;

        public string ParametersSummary
        {
            get
            {
                if (_rule.Parameters == null || !_rule.Parameters.Any())
                    return "No configurable parameters";

                var parts = _rule.Parameters.Select(kvp => $"{kvp.Key}: {kvp.Value}");
                return string.Join(" | ", parts);
            }
        }

        public RuleViewModel(IRule rule)
        {
            _rule = rule;
            _isEnabled = rule.IsEnabled;
        }

        partial void OnIsEnabledChanged(bool value)
        {
            _rule.IsEnabled = value;
        }

        public void RefreshParameters()
        {
            OnPropertyChanged(nameof(ParametersSummary));
        }
    }
}
