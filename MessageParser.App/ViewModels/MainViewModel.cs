using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MessageParser.Core;
using MessageParser.Core.Bus;
using MessageParser.Core.Messages;
using MessageParser.Core.Simulation;
using MessageParser.Core.Filtering;
using MessageParser.App.Models;
using MessageParser.Plugins;
using Avalonia.Threading;

namespace MessageParser.App.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly DataBus _dataBus = new();
    private readonly TestMessageLink _messageLink = new();
    private readonly List<IFilterInfo> _discoveredFilterInfos = new();
    
    private readonly List<MessageBase> _allMessages = new();
    private readonly List<int> _filteredIndices = new();
    private readonly MessageRegistry _messageRegistry = new();
    private readonly HashSet<string> _publishedMessageTypes = new(StringComparer.OrdinalIgnoreCase);

    private IDisposable? _messageSubscription;
    private IDisposable? _filterInfoSubscription;

    [ObservableProperty]
    private string _sender = string.Empty;

    [ObservableProperty]
    private string _receiver = string.Empty;

    [ObservableProperty]
    private string _commandName = string.Empty;

    [ObservableProperty]
    private bool _isSimulationRunning;

    [ObservableProperty]
    private string _simulationButtonText = "Start Simulator";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private SimulatedMessageItemViewModel? _selectedMessage;

    [ObservableProperty]
    private bool _hasSelectedMessage;

    [ObservableProperty]
    private string _availablePropertiesHint = "Sender, Receiver";

    public ObservableCollection<PayloadItemViewModel> PayloadItems { get; } = new();

    [ObservableProperty]
    private IList _simulatedMessages;

    public MainViewModel()
    {
        // Initialize virtualized collection
        _simulatedMessages = new VirtualizedMessageList(_allMessages, _filteredIndices, _messageRegistry);

        // Subscribe to MessageBase (native messages) and IFilterInfo (message metadata)
        _messageSubscription = _dataBus.Subscribe<MessageBase>(OnMessageReceived);
        _filterInfoSubscription = _dataBus.Subscribe<IFilterInfo>(OnFilterInfoReceived);

        // Instantiate and register default plugin (statically for this demo)
        var basePlugin = new BaseMessagesPlugin();
        basePlugin.Initialize(_messageRegistry);

        // Bind simulator event
        _messageLink.RawMessageReceived += OnSimulatorRawMessageReceived;
    }

    private void OnSimulatorRawMessageReceived(object? sender, string rawMessage)
    {
        var parsed = Parser.Parse(rawMessage);
        if (parsed.IsValid)
        {
            var typedMessage = _messageRegistry.Parse(parsed) as MessageBase;
            if (typedMessage != null)
            {
                var schema = _messageRegistry.GetSchema(typedMessage.GetType().Name);
                if (schema != null)
                {
                    _dataBus.Publish<MessageBase>(typedMessage);
                }
            }
        }
    }

    private void OnFilterInfoReceived(IFilterInfo filterInfo)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_discoveredFilterInfos.Any(fi => fi.MessageTypeName == filterInfo.MessageTypeName))
            {
                _discoveredFilterInfos.Add(filterInfo);
                UpdateAvailablePropertiesHint();
            }
        });
    }

    private void UpdateAvailablePropertiesHint()
    {
        var properties = _discoveredFilterInfos
            .SelectMany(fi => fi.FilterableProperties.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p)
            .ToList();

        // Always put Sender and Receiver first if present
        properties.Remove("Sender");
        properties.Remove("Receiver");
        properties.Insert(0, "Sender");
        properties.Insert(1, "Receiver");

        AvailablePropertiesHint = string.Join(", ", properties);
    }

    private void OnMessageReceived(MessageBase message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            string typeName = message.GetType().Name;
            lock (_publishedMessageTypes)
            {
                if (_publishedMessageTypes.Add(typeName))
                {
                    var schema = _messageRegistry.GetSchema(typeName);
                    if (schema != null)
                    {
                        _dataBus.Publish<IFilterInfo>(schema);
                    }
                }
            }

            lock (_allMessages)
            {
                _allMessages.Add(message);
                int newIndex = _allMessages.Count - 1;

                if (MatchesFilter(message))
                {
                    _filteredIndices.Add(newIndex);
                    int filteredIndex = _filteredIndices.Count - 1;
                    (SimulatedMessages as VirtualizedMessageList)?.NotifyItemAdded(filteredIndex);
                }
            }
        });
    }

    private bool MatchesFilter(MessageBase message)
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return true;

        var query = QueryParser.Parse(SearchQuery);
        if (query.IsValid)
        {
            string typeName = message.GetType().Name;
            var filterInfo = _discoveredFilterInfos.FirstOrDefault(fi => fi.MessageTypeName == typeName);
            if (filterInfo == null) return false;

            var propEntry = filterInfo.FilterableProperties.FirstOrDefault(p => string.Equals(p.Key, query.PropertyName, StringComparison.OrdinalIgnoreCase));
            if (propEntry.Key == null)
            {
                object? fallbackVal = query.PropertyName.ToUpperInvariant() switch
                {
                    "SENDER" => message.Sender,
                    "RECEIVER" => message.Receiver,
                    "MESSAGETYPE" or "TYPE" => typeName,
                    _ => null
                };
                if (fallbackVal is string fs)
                {
                    var fallbackPredicate = GetPredicateForOperator(typeof(string), query.Operator, query.Value);
                    return fallbackPredicate(fs);
                }
                return false;
            }

            var propType = propEntry.Value;
            var predicate = GetPredicateForOperator(propType, query.Operator, query.Value);
            return _messageRegistry.EvaluateFilter(message, propEntry.Key, predicate);
        }
        else
        {
            string text = SearchQuery.Trim();
            string typeName = message.GetType().Name;
            if (message.Sender.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                message.Receiver.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var schema = _messageRegistry.GetSchema(typeName);
            if (schema != null)
            {
                var summary = schema.SummaryFormatter(message);
                return summary.Contains(text, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }

    private Func<MessageBase, bool> CompileFilter(string queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return _ => true;
        }

        var query = QueryParser.Parse(queryText);
        if (query.IsValid)
        {
            return message =>
            {
                string typeName = message.GetType().Name;
                var info = _discoveredFilterInfos.FirstOrDefault(fi => fi.MessageTypeName == typeName);
                if (info == null) return false;

                var propEntry = info.FilterableProperties.FirstOrDefault(p => string.Equals(p.Key, query.PropertyName, StringComparison.OrdinalIgnoreCase));
                if (propEntry.Key == null)
                {
                    object? fallbackVal = query.PropertyName.ToUpperInvariant() switch
                    {
                        "SENDER" => message.Sender,
                        "RECEIVER" => message.Receiver,
                        "MESSAGETYPE" or "TYPE" => typeName,
                        _ => null
                    };
                    if (fallbackVal is string fs)
                    {
                        var fallbackPredicate = GetPredicateForOperator(typeof(string), query.Operator, query.Value);
                        return fallbackPredicate(fs);
                    }
                    return false;
                }

                var propType = propEntry.Value;
                var predicate = GetPredicateForOperator(propType, query.Operator, query.Value);
                return _messageRegistry.EvaluateFilter(message, propEntry.Key, predicate);
            };
        }
        else
        {
            string text = queryText.Trim();
            return message =>
            {
                string typeName = message.GetType().Name;
                if (message.Sender.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    message.Receiver.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    typeName.Contains(text, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var schema = _messageRegistry.GetSchema(typeName);
                if (schema != null)
                {
                    var summary = schema.SummaryFormatter(message);
                    return summary.Contains(text, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            };
        }
    }

    private async Task RefreshFilteredMessagesAsync()
    {
        string currentQuery = SearchQuery;
        var filterPredicate = CompileFilter(currentQuery);

        var matched = await Task.Run(() =>
        {
            var results = new List<int>();
            lock (_allMessages)
            {
                for (int i = 0; i < _allMessages.Count; i++)
                {
                    if (filterPredicate(_allMessages[i]))
                    {
                        results.Add(i);
                    }
                }
            }
            return results;
        });

        lock (_allMessages)
        {
            _filteredIndices.Clear();
            _filteredIndices.AddRange(matched);
        }

        Dispatcher.UIThread.Post(() =>
        {
            (SimulatedMessages as VirtualizedMessageList)?.NotifyReset();

            if (SelectedMessage != null && !SimulatedMessages.Cast<SimulatedMessageItemViewModel>().Contains(SelectedMessage))
            {
                SelectedMessage = null;
            }
        });
    }

    private Func<object?, bool> GetPredicateForOperator(Type propertyType, string op, string filterVal)
    {
        string opUpper = op.ToUpperInvariant();

        if (propertyType == typeof(double))
        {
            if (double.TryParse(filterVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double targetDouble))
            {
                return val =>
                {
                    if (val is double d)
                    {
                        return opUpper switch
                        {
                            ">" => d > targetDouble,
                            "<" => d < targetDouble,
                            ">=" => d >= targetDouble,
                            "<=" => d <= targetDouble,
                            "==" or "=" => Math.Abs(d - targetDouble) < 0.0001,
                            "!=" => Math.Abs(d - targetDouble) >= 0.0001,
                            _ => true
                        };
                    }
                    return false;
                };
            }
        }
        else // string
        {
            return val =>
            {
                if (val is string s)
                {
                    return opUpper switch
                    {
                        "CONTAINS" => s.Contains(filterVal, StringComparison.OrdinalIgnoreCase),
                        "STARTSWITH" => s.StartsWith(filterVal, StringComparison.OrdinalIgnoreCase),
                        "ENDSWITH" => s.EndsWith(filterVal, StringComparison.OrdinalIgnoreCase),
                        "==" or "=" => string.Equals(s, filterVal, StringComparison.OrdinalIgnoreCase),
                        "!=" => !string.Equals(s, filterVal, StringComparison.OrdinalIgnoreCase),
                        _ => true
                    };
                }
                return false;
            };
        }

        return _ => true;
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = RefreshFilteredMessagesAsync();
    }

    partial void OnSelectedMessageChanged(SimulatedMessageItemViewModel? value)
    {
        PayloadItems.Clear();
        HasSelectedMessage = value != null;

        if (value == null)
        {
            Sender = string.Empty;
            Receiver = string.Empty;
            CommandName = string.Empty;
            return;
        }

        Sender = value.Sender;
        Receiver = value.Receiver;
        CommandName = value.Type;

        var msg = value.Message;
        var schema = _messageRegistry.GetSchema(value.Type);
        if (schema != null)
        {
            var fields = schema.PayloadExtractor(msg);
            foreach (var field in fields)
            {
                AddPayload(field.Key, field.Value);
            }
        }
    }

    private void AddPayload(string key, string value)
    {
        PayloadItems.Add(new PayloadItemViewModel(key, value));
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    private void ToggleSimulation()
    {
        if (IsSimulationRunning)
        {
            _messageLink.Stop();
            IsSimulationRunning = false;
            SimulationButtonText = "Start Simulator";
        }
        else
        {
            _messageLink.Start();
            IsSimulationRunning = true;
            SimulationButtonText = "Stop Simulator";
        }
    }

    public void Dispose()
    {
        _messageSubscription?.Dispose();
        _filterInfoSubscription?.Dispose();
        _messageLink.Stop();
    }
}

public class PayloadItemViewModel
{
    public string Key { get; }
    public string Value { get; }

    public PayloadItemViewModel(string key, string value)
    {
        Key = key;
        Value = value;
    }
}

public class SimulatedMessageItemViewModel
{
    public string Time { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Receiver { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public MessageBase Message { get; set; } = null!;
}
