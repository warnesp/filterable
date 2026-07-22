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
    /// <summary>
    /// An allocation-free, node-recycling Least-Recently-Used (LRU) Cache.
    /// Uses a custom doubly-linked list of internal <see cref="CacheNode"/> instances and a dictionary for O(1) lookups.
    /// Once maximum capacity is reached, evicted tail nodes are recycled in-place, eliminating GC allocations during cache operations.
    /// </summary>
    /// <typeparam name="TKey">The key type for cached entries.</typeparam>
    /// <typeparam name="TValue">The value type for cached entries.</typeparam>
    public class LruCache<TKey, TValue> where TKey : notnull
    {
        private class CacheNode
        {
            public TKey Key;
            public TValue Value;
            public CacheNode? Next;
            public CacheNode? Prev;

            public CacheNode(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }
        }

        private readonly int _capacity;
        private readonly Dictionary<TKey, CacheNode> _map;
        private CacheNode? _head;
        private CacheNode? _tail;

        /// <summary>
        /// Initializes a new instance of the <see cref="LruCache{TKey, TValue}"/> class with the specified capacity limit.
        /// </summary>
        /// <param name="capacity">Maximum number of items the cache will hold before recycling least-recently used nodes.</param>
        public LruCache(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
            _capacity = capacity;
            _map = new Dictionary<TKey, CacheNode>(capacity);
        }

        /// <summary>
        /// Attempts to retrieve a cached value by key. If found, moves the node to the front of the LRU list.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">When found, contains the cached value; otherwise, default.</param>
        /// <returns><c>true</c> if the key was found in cache; otherwise, <c>false</c>.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_map.TryGetValue(key, out var node))
            {
                MoveToHead(node);
                value = node.Value;
                return true;
            }
            value = default!;
            return false;
        }

        /// <summary>
        /// Adds or updates a key/value pair in the cache.
        /// If capacity is reached, the evicted tail node is recycled in-place without heap allocations.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        public void Add(TKey key, TValue value)
        {
            if (_map.TryGetValue(key, out var node))
            {
                node.Value = value;
                MoveToHead(node);
                return;
            }

            if (_map.Count >= _capacity && _tail != null)
            {
                // Recycle the tail node to avoid GC allocations
                node = _tail;
                _map.Remove(node.Key);

                node.Key = key;
                node.Value = value;

                _map[key] = node;
                MoveToHead(node);
            }
            else
            {
                // Cache not yet at capacity
                node = new CacheNode(key, value);
                _map[key] = node;
                AddToHead(node);
            }
        }

        private void MoveToHead(CacheNode node)
        {
            if (node == _head) return;

            // Unlink node from current position
            if (node.Prev != null) node.Prev.Next = node.Next;
            if (node.Next != null) node.Next.Prev = node.Prev;

            if (node == _tail)
            {
                _tail = node.Prev;
            }

            // Link at head
            node.Next = _head;
            node.Prev = null;

            if (_head != null)
            {
                _head.Prev = node;
            }

            _head = node;

            if (_tail == null)
            {
                _tail = node;
            }
        }

        private void AddToHead(CacheNode node)
        {
            node.Next = _head;
            node.Prev = null;

            if (_head != null)
            {
                _head.Prev = node;
            }

            _head = node;

            if (_tail == null)
            {
                _tail = node;
            }
        }

        /// <summary>
        /// Clears all entries from the cache.
        /// </summary>
        public void Clear()
        {
            _map.Clear();
            _head = null;
            _tail = null;
        }
    }

    /// <summary>
    /// A virtualized, read-only collection designed for Avalonia UI list controls.
    /// <para>
    /// Rather than instantiating and keeping thousands of <see cref="SimulatedMessageItemViewModel"/> objects in memory,
    /// this class acts as an index-mapped virtual view over a raw list of <see cref="MessageBase"/> instances filtered by index.
    /// </para>
    /// ViewModels are lazily constructed on demand as the UI scrolls, and cached using an internal <see cref="LruCache{TKey, TValue}"/>
    /// to avoid redundant garbage collection pressure.
    /// </summary>
    public class VirtualizedMessageList : IList, IReadOnlyList<SimulatedMessageItemViewModel>, INotifyCollectionChanged
    {
        private readonly IReadOnlyList<MessageBase> _allMessages;
        private readonly IReadOnlyList<int> _filteredIndices;
        private readonly MessageRegistry _registry;
        private readonly LruCache<int, SimulatedMessageItemViewModel> _cache = new(100);

        /// <summary>
        /// Occurs when the collection changes (items added, or entire list reset).
        /// </summary>
        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualizedMessageList"/> class.
        /// </summary>
        /// <param name="allMessages">The full backing collection of raw messages.</param>
        /// <param name="filteredIndices">An indirect index map containing positions in <paramref name="allMessages"/> that match current search criteria.</param>
        /// <param name="registry">The central message registry used to obtain summary formatters and schemas.</param>
        public VirtualizedMessageList(IReadOnlyList<MessageBase> allMessages, IReadOnlyList<int> filteredIndices, MessageRegistry registry)
        {
            _allMessages = allMessages ?? throw new ArgumentNullException(nameof(allMessages));
            _filteredIndices = filteredIndices ?? throw new ArgumentNullException(nameof(filteredIndices));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Clears the LRU ViewModel cache and notifies subscribers (UI) that the collection has been reset.
        /// Call this whenever search filters change or a complete refresh occurs.
        /// </summary>
        public void NotifyReset()
        {
            _cache.Clear();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Notifies subscribers (UI) that a single item has been appended at the specified filtered index position.
        /// </summary>
        /// <param name="index">The 0-based index in the filtered collection where the item was added.</param>
        public void NotifyItemAdded(int index)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add, 
                this[index], 
                index));
        }

        /// <summary>
        /// Gets the total number of filtered messages currently matching the active search query.
        /// </summary>
        public int Count => _filteredIndices.Count;

        /// <summary>
        /// Indexer that retrieves or constructs a <see cref="SimulatedMessageItemViewModel"/> for the specified index.
        /// First checks the LRU cache; if missed, lazily formats and constructs the ViewModel from the underlying <see cref="MessageBase"/>.
        /// </summary>
        /// <param name="index">The index in the filtered message list (0 to <see cref="Count"/> - 1).</param>
        /// <returns>The constructed or cached <see cref="SimulatedMessageItemViewModel"/>.</returns>
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

        /// <inheritdoc/>
        public bool IsReadOnly => true;

        /// <inheritdoc/>
        public bool IsFixedSize => true;

        /// <inheritdoc/>
        public int Add(object? value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public void Clear() => throw new NotSupportedException();

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void Insert(int index, object? value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public void Remove(object? value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public void RemoveAt(int index) => throw new NotSupportedException();

        /// <inheritdoc/>
        public void CopyTo(Array array, int index)
        {
            for (int i = 0; i < Count; i++)
            {
                array.SetValue(this[i], index + i);
            }
        }

        /// <inheritdoc/>
        public bool IsSynchronized => false;

        /// <inheritdoc/>
        public object SyncRoot => this;

        /// <inheritdoc/>
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
