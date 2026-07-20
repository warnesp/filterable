using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MessageParser.Core;
using MessageParser.Core.Bus;
using MessageParser.Core.Messages;
using MessageParser.Core.Simulation;
using MessageParser.Core.Filtering;
using Avalonia.Threading;

namespace MessageParser.App.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly DataBus _dataBus = new();
    private readonly TestMessageLink _messageLink = new();
    private readonly FilterBusListenerRegistry _filterRegistry;
    private readonly List<IFilterInfo> _discoveredFilterInfos = new();
    private readonly List<SimulatedMessageItemViewModel> _allSimulatedMessages = new();
    
    private IDisposable? _filterableSubscription;
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

    public ObservableCollection<PayloadItemViewModel> PayloadItems { get; } = new();
    public ObservableCollection<SimulatedMessageItemViewModel> SimulatedMessages { get; } = new();

    public MainViewModel()
    {
        // Initialize and register filter decorators
        _filterRegistry = new FilterBusListenerRegistry(_dataBus);

        // Subscribe to IFilterable (decorated messages) and IFilterInfo (message metadata)
        _filterableSubscription = _dataBus.Subscribe<IFilterable>(OnFilterableMessageReceived);
        _filterInfoSubscription = _dataBus.Subscribe<IFilterInfo>(OnFilterInfoReceived);

        // Bind simulator event
        _messageLink.RawMessageReceived += OnSimulatorRawMessageReceived;
    }

    private void OnSimulatorRawMessageReceived(object? sender, string rawMessage)
    {
        var parsed = Parser.Parse(rawMessage);
        if (parsed.IsValid)
        {
            var typedMessage = parsed.ToTypedMessage();
            if (typedMessage != null)
            {
                _dataBus.Publish(typedMessage);
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
            }
        });
    }

    private void OnFilterableMessageReceived(IFilterable filterable)
    {
        Dispatcher.UIThread.Post(() =>
        {
            string detailsSummary = filterable.WrappedMessage switch
            {
                AirTrack air => $"Callsign: {air.Callsign}, Alt: {air.Altitude}ft, Speed: {air.Speed}kts",
                GroundTrack ground => $"Unit: {ground.UnitId}, Type: {ground.Type}, Speed: {ground.Speed}mph",
                HeartBeat hb => $"Device: {hb.DeviceId}, Status: {hb.Status}, Battery: {hb.Battery}",
                SelfLocation self => $"GPS: {self.GpsLock}, Lat/Lon: {self.Latitude:F3}/{self.Longitude:F3}, Alt: {self.Altitude}m",
                GeneratorStatus gen => $"Gen: {gen.GenId}, State: {gen.State}, Load: {gen.Load}, Fuel: {gen.FuelLevel}",
                _ => "Unknown typed message"
            };

            var item = new SimulatedMessageItemViewModel
            {
                Time = filterable.ReceivedTime.ToLocalTime().ToString("HH:mm:ss.fff"),
                Type = filterable.MessageTypeName,
                Sender = filterable.Sender,
                Receiver = filterable.Receiver,
                Summary = detailsSummary,
                Filterable = filterable
            };

            // Add to in-memory list (capped at 100)
            _allSimulatedMessages.Insert(0, item);
            if (_allSimulatedMessages.Count > 100)
            {
                _allSimulatedMessages.RemoveAt(_allSimulatedMessages.Count - 1);
            }

            // If it passes current filter, display it in the ListBox
            if (MatchesFilter(filterable, detailsSummary))
            {
                SimulatedMessages.Insert(0, item);
                if (SimulatedMessages.Count > 100)
                {
                    SimulatedMessages.RemoveAt(SimulatedMessages.Count - 1);
                }
            }
        });
    }

    private bool MatchesFilter(IFilterable filterable, string summary)
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return true;

        var query = QueryParser.Parse(SearchQuery);
        if (query.IsValid)
        {
            var filterInfo = _discoveredFilterInfos.FirstOrDefault(fi => fi.MessageTypeName == filterable.MessageTypeName);
            if (filterInfo == null) return false;

            var propEntry = filterInfo.FilterableProperties.FirstOrDefault(p => string.Equals(p.Key, query.PropertyName, StringComparison.OrdinalIgnoreCase));
            if (propEntry.Key == null)
            {
                return false;
            }

            var propType = propEntry.Value;
            var predicate = GetPredicateForOperator(propType, query.Operator, query.Value);
            return filterable.ApplyFilter(propEntry.Key, predicate);
        }
        else
        {
            string text = SearchQuery.Trim();
            return filterable.Sender.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                   filterable.Receiver.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                   filterable.MessageTypeName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                   summary.Contains(text, StringComparison.OrdinalIgnoreCase);
        }
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

    private void RefreshFilteredMessages()
    {
        SimulatedMessages.Clear();
        foreach (var msg in _allSimulatedMessages)
        {
            if (MatchesFilter(msg.Filterable, msg.Summary))
            {
                SimulatedMessages.Add(msg);
            }
        }

        // Clear details selection if it no longer matches the filter
        if (SelectedMessage != null && !SimulatedMessages.Contains(SelectedMessage))
        {
            SelectedMessage = null;
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        RefreshFilteredMessages();
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

        var msg = value.Filterable.WrappedMessage;
        switch (msg)
        {
            case AirTrack air:
                AddPayload("Callsign", air.Callsign);
                AddPayload("Latitude", air.Latitude.ToString("F6"));
                AddPayload("Longitude", air.Longitude.ToString("F6"));
                AddPayload("Altitude", $"{air.Altitude} ft");
                AddPayload("Speed", $"{air.Speed} kts");
                AddPayload("Heading", $"{air.Heading}°");
                AddPayload("Squawk", air.Squawk);
                break;
            case GroundTrack ground:
                AddPayload("Unit ID", ground.UnitId);
                AddPayload("Latitude", ground.Latitude.ToString("F6"));
                AddPayload("Longitude", ground.Longitude.ToString("F6"));
                AddPayload("Speed", $"{ground.Speed} mph");
                AddPayload("Heading", $"{ground.Heading}°");
                AddPayload("Type", ground.Type);
                break;
            case HeartBeat hb:
                AddPayload("Device ID", hb.DeviceId);
                AddPayload("Status", hb.Status);
                AddPayload("Uptime", hb.Uptime);
                AddPayload("Battery", hb.Battery);
                break;
            case SelfLocation self:
                AddPayload("Latitude", self.Latitude.ToString("F6"));
                AddPayload("Longitude", self.Longitude.ToString("F6"));
                AddPayload("Altitude", $"{self.Altitude} m");
                AddPayload("GPS Lock", self.GpsLock);
                AddPayload("Precision", self.Precision);
                break;
            case GeneratorStatus gen:
                AddPayload("Generator ID", gen.GenId);
                AddPayload("State", gen.State);
                AddPayload("Load", gen.Load);
                AddPayload("Fuel Level", gen.FuelLevel);
                AddPayload("Temperature", gen.Temperature);
                break;
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
        _filterableSubscription?.Dispose();
        _filterInfoSubscription?.Dispose();
        _filterRegistry.Dispose();
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
    public IFilterable Filterable { get; set; } = null!;
}
