using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using MessageParser.Core;
using MessageParser.Core.Filtering;
using MessageParser.Core.Messages;
using MessageParser.App.ViewModels;

namespace MessageParser.App.Models
{
    public class LruCache<TKey, TValue> where TKey : notnull
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _map = new();
        private readonly LinkedList<KeyValuePair<TKey, TValue>> _list = new();

        public LruCache(int capacity)
        {
            _capacity = capacity;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _list.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
            value = default!;
            return false;
        }

        public void Add(TKey key, TValue value)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _map.Remove(key);
            }
            else if (_map.Count >= _capacity)
            {
                var lastNode = _list.Last;
                if (lastNode != null)
                {
                    _list.RemoveLast();
                    _map.Remove(lastNode.Value.Key);
                }
            }

            var newNode = new LinkedListNode<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(key, value));
            _list.AddFirst(newNode);
            _map[key] = newNode;
        }

        public void Clear()
        {
            _map.Clear();
            _list.Clear();
        }
    }

    public class VirtualizedMessageList : IList, IReadOnlyList<SimulatedMessageItemViewModel>, INotifyCollectionChanged
    {
        private readonly IReadOnlyList<MessageBase> _allMessages;
        private readonly IReadOnlyList<int> _filteredIndices;
        private readonly MessageRegistry _registry;
        private readonly LruCache<int, SimulatedMessageItemViewModel> _cache = new(100);

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public VirtualizedMessageList(IReadOnlyList<MessageBase> allMessages, IReadOnlyList<int> filteredIndices, MessageRegistry registry)
        {
            _allMessages = allMessages ?? throw new ArgumentNullException(nameof(allMessages));
            _filteredIndices = filteredIndices ?? throw new ArgumentNullException(nameof(filteredIndices));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void NotifyReset()
        {
            _cache.Clear();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void NotifyItemAdded(int index)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add, 
                this[index], 
                index));
        }

        public int Count => _filteredIndices.Count;

        public SimulatedMessageItemViewModel this[int index]
        {
            get
            {
                if (index < 0 || index >= _filteredIndices.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                int sourceIndex = _filteredIndices[index];
                if (_cache.TryGetValue(sourceIndex, out var cachedVm))
                {
                    return cachedVm;
                }

                var message = _allMessages[sourceIndex];
                string typeName = message.GetType().Name;
                var schema = _registry.GetSchema(typeName);

                string summary = schema?.SummaryFormatter(message) ?? "Unknown message type";

                var vm = new SimulatedMessageItemViewModel
                {
                    Time = message.ReceivedTime.ToLocalTime().ToString("HH:mm:ss.fff"),
                    Type = typeName,
                    Sender = message.Sender,
                    Receiver = message.Receiver,
                    Summary = summary,
                    Message = message
                };

                _cache.Add(sourceIndex, vm);
                return vm;
            }
            set => throw new NotSupportedException();
        }

        object? IList.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException();
        }

        public bool IsReadOnly => true;
        public bool IsFixedSize => true;

        public int Add(object? value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        
        public bool Contains(object? value)
        {
            if (value is SimulatedMessageItemViewModel vm)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (this[i] == vm) return true;
                }
            }
            return false;
        }

        public int IndexOf(object? value)
        {
            if (value is SimulatedMessageItemViewModel vm)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (this[i] == vm) return i;
                }
            }
            return -1;
        }

        public void Insert(int index, object? value) => throw new NotSupportedException();
        public void Remove(object? value) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();

        public void CopyTo(Array array, int index)
        {
            for (int i = 0; i < Count; i++)
            {
                array.SetValue(this[i], index + i);
            }
        }

        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public IEnumerator<SimulatedMessageItemViewModel> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
