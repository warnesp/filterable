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
    private string _rawMessage = string.Empty;

    [ObservableProperty]
    private string _sender = string.Empty;

    [ObservableProperty]
    private string _receiver = string.Empty;

    [ObservableProperty]
    private string _commandName = string.Empty;

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private string _statusMessage = "Enter a message to parse.";

    [ObservableProperty]
    private string _statusBackground = "#1A1A1E";

    [ObservableProperty]
    private string _statusForeground = "#ECECF1";

    [ObservableProperty]
    private bool _isSimulationRunning;

    [ObservableProperty]
    private string _simulationButtonText = "Start Simulator";

    // Search bar query property
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    public ObservableCollection<PayloadItemViewModel> PayloadItems { get; } = new();
    public ObservableCollection<SimulatedMessageItemViewModel> SimulatedMessages { get; } = new();

    public MainViewModel()
    {
        // Set a default sample message
        RawMessage = "CLIENT -> SERVER | LOGIN | username=alice;version=1.0;timestamp=1718816823";

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

            // Find property case-insensitively
            var propEntry = filterInfo.FilterableProperties.FirstOrDefault(p => string.Equals(p.Key, query.PropertyName, StringComparison.OrdinalIgnoreCase));
            if (propEntry.Key == null)
            {
                // Property not found on this message type, exclude it
                return false;
            }

            var propType = propEntry.Value;
            var predicate = GetPredicateForOperator(propType, query.Operator, query.Value);
            return filterable.ApplyFilter(propEntry.Key, predicate);
        }
        else
        {
            // Fallback: simple text search on summary and routing properties
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
    }

    partial void OnSearchQueryChanged(string value)
    {
        RefreshFilteredMessages();
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

    partial void OnRawMessageChanged(string value)
    {
        ParseMessage();
    }

    [RelayCommand]
    private void ParseMessage()
    {
        var parsed = Parser.Parse(RawMessage);
        Sender = parsed.Sender;
        Receiver = parsed.Receiver;
        CommandName = parsed.Command;
        IsValid = parsed.IsValid;

        PayloadItems.Clear();
        foreach (var kvp in parsed.Payload)
        {
            PayloadItems.Add(new PayloadItemViewModel(kvp.Key, kvp.Value));
        }

        if (parsed.IsValid)
        {
            StatusMessage = "✓ Message successfully parsed!";
            StatusBackground = "#15241E"; // Muted green
            StatusForeground = "#86E0A3"; // Bright green
        }
        else
        {
            StatusMessage = string.IsNullOrEmpty(parsed.Error) ? "Invalid message format." : $"✗ {parsed.Error}";
            StatusBackground = "#2D1F21"; // Muted red
            StatusForeground = "#F88F92"; // Bright red
        }
    }

    [RelayCommand]
    private void LoadSample(string sampleType)
    {
        RawMessage = sampleType switch
        {
            "airtrack" => "AWACS -> HQ | AIR_TRACK | callsign=AF101;lat=34.0522;lon=-118.2437;alt=35000;speed=460;heading=090;squawk=1200",
            "groundtrack" => "SCOUT_01 -> HQ | GROUND_TRACK | unit_id=T72_05;lat=34.0532;lon=-118.2447;speed=35;heading=180;type=Tank",
            "heartbeat" => "DRONE_01 -> C2 | HEARTBEAT | device_id=DRN-99;status=NOMINAL;uptime=1420s;battery=88%",
            "selflocation" => "SOLDIER_03 -> HQ | SELF_LOCATION | lat=34.0512;lon=-118.2427;alt=280;gps_lock=3D;precision=1.2m",
            "generatorstatus" => "BASE_GEN -> MONITOR | GENERATOR_STATUS | gen_id=GEN_B4;state=RUNNING;load=78%;fuel=92%;temp=75.4C",
            "invalid" => "INVALID_MESSAGE_WITHOUT_PIPES",
            _ => string.Empty
        };
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
