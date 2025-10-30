using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FluffyByte.Tools.FluffyTypes;

/// <summary>
/// Represents a thread-safe dictionary that allows concurrent access to key-value pairs.
/// </summary>
/// <remarks>This dictionary provides thread-safe operations for adding, removing, and accessing key-value pairs.
/// All operations that modify or access the dictionary are synchronized using a lock to ensure thread safety.  Use this
/// class when multiple threads need to access and modify a shared dictionary concurrently. Note that enumeration
/// creates a snapshot of the dictionary to avoid holding the lock during iteration.</remarks>
/// <typeparam name="TKey">The type of the keys in the dictionary. Keys must be non-null.</typeparam>
/// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
public class ThreadSafeDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _dictionary = [];
    private readonly object _lock = new();

    /// <summary>
    /// Gets the number of key-value pairs in the dictionary.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _dictionary.Count;
            }
        }
    }

    /// <summary>
    /// Gets a collection containing the keys in the dictionary.
    /// </summary>
    /// <remarks>Returns a snapshot to avoid holding the lock during enumeration.</remarks>
    public ICollection<TKey> Keys
    {
        get
        {
            lock (_lock)
            {
                return [.. _dictionary.Keys];
            }
        }
    }

    /// <summary>
    /// Gets a collection containing the values in the dictionary.
    /// </summary>
    /// <remarks>Returns a snapshot to avoid holding the lock during enumeration.</remarks>
    public ICollection<TValue> Values
    {
        get
        {
            lock (_lock)
            {
                return [.. _dictionary.Values];
            }
        }
    }

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when getting a key that doesn't exist.</exception>
    public TValue this[TKey key]
    {
        get
        {
            lock (_lock)
            {
                return _dictionary[key];
            }
        }
        set
        {
            lock (_lock)
            {
                _dictionary[key] = value;
            }
        }
    }

    /// <summary>
    /// Adds the specified key and value to the dictionary.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the key already exists.</exception>
    public void Add(TKey key, TValue value)
    {
        lock (_lock)
        {
            _dictionary.Add(key, value);
        }
    }

    /// <summary>
    /// Adds a key-value pair to the dictionary if the key does not already exist.
    /// </summary>
    /// <returns>True if the key-value pair was added; false if the key already exists.</returns>
    public bool TryAdd(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_dictionary.ContainsKey(key))
                return false;

            _dictionary.Add(key, value);
            return true;
        }
    }

    /// <summary>
    /// Removes the value with the specified key from the dictionary.
    /// </summary>
    /// <returns>True if the element was removed; false if the key was not found.</returns>
    public bool Remove(TKey key)
    {
        lock (_lock)
        {
            return _dictionary.Remove(key);
        }
    }

    /// <summary>
    /// Removes the value with the specified key and returns it via the out parameter.
    /// </summary>
    /// <returns>True if the element was found and removed; false otherwise.</returns>
    public bool Remove(TKey key, out TValue? value)
    {
        lock (_lock)
        {
            if (_dictionary.TryGetValue(key, out value))
            {
                _dictionary.Remove(key);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Removes all keys and values from the dictionary.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _dictionary.Clear();
        }
    }

    /// <summary>
    /// Determines whether the dictionary contains the specified key.
    /// </summary>
    public bool ContainsKey(TKey key)
    {
        lock (_lock)
        {
            return _dictionary.ContainsKey(key);
        }
    }

    /// <summary>
    /// Determines whether the dictionary contains a specific value.
    /// </summary>
    public bool ContainsValue(TValue value)
    {
        lock (_lock)
        {
            return _dictionary.ContainsValue(value);
        }
    }

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <returns>True if the key was found; false otherwise.</returns>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        lock (_lock)
        {
            return _dictionary.TryGetValue(key, out value);
        }
    }

    /// <summary>
    /// Gets the value associated with the specified key, or a default value if not found.
    /// </summary>
    public TValue? GetValueOrDefault(TKey key, TValue? defaultValue = default)
    {
        lock (_lock)
        {
            return _dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }

    /// <summary>
    /// Adds a key-value pair if the key doesn't exist, or updates the value if it does.
    /// </summary>
    /// <returns>The new value (either added or updated).</returns>
    public TValue AddOrUpdate(TKey key, TValue value)
    {
        lock (_lock)
        {
            _dictionary[key] = value;
            return value;
        }
    }

    /// <summary>
    /// Adds a key-value pair if the key doesn't exist, or updates it using the update function if it does.
    /// </summary>
    /// <returns>The new value (either added or updated).</returns>
    public TValue AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory)
    {
        lock (_lock)
        {
            if (_dictionary.TryGetValue(key, out var existingValue))
            {
                var newValue = updateValueFactory(key, existingValue);
                _dictionary[key] = newValue;
                return newValue;
            }
            else
            {
                _dictionary[key] = addValue;
                return addValue;
            }
        }
    }

    /// <summary>
    /// Gets the value for a key, or adds it if it doesn't exist.
    /// </summary>
    public TValue GetOrAdd(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_dictionary.TryGetValue(key, out var existingValue))
                return existingValue;

            _dictionary[key] = value;
            return value;
        }
    }

    /// <summary>
    /// Gets the value for a key, or adds it using a factory function if it doesn't exist.
    /// </summary>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        lock (_lock)
        {
            if (_dictionary.TryGetValue(key, out var existingValue))
                return existingValue;

            var newValue = valueFactory(key);
            _dictionary[key] = newValue;
            return newValue;
        }
    }

    /// <summary>
    /// Filters the dictionary based on a predicate and returns matching key-value pairs.
    /// </summary>
    public Dictionary<TKey, TValue> Where(Func<KeyValuePair<TKey, TValue>, bool> predicate)
    {
        lock (_lock)
        {
            return _dictionary.Where(predicate).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }

    /// <summary>
    /// Filters the dictionary values based on a predicate.
    /// </summary>
    public Dictionary<TKey, TValue> WhereValue(Func<TValue, bool> predicate)
    {
        lock (_lock)
        {
            return _dictionary.Where(kvp => predicate(kvp.Value)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }

    /// <summary>
    /// Projects each value into a new form using the selector function.
    /// </summary>
    public Dictionary<TKey, TResult> Select<TResult>(Func<TValue, TResult> selector)
    {
        lock (_lock)
        {
            return _dictionary.ToDictionary(kvp => kvp.Key, kvp => selector(kvp.Value));
        }
    }

    /// <summary>
    /// Returns the first value that matches the predicate, or default if none found.
    /// </summary>
    public TValue? FirstOrDefault(Func<TValue, bool> predicate)
    {
        lock (_lock)
        {
            return _dictionary.Values.FirstOrDefault(predicate);
        }
    }

    /// <summary>
    /// Determines whether any value matches the predicate.
    /// </summary>
    public bool Any(Func<TValue, bool> predicate)
    {
        lock (_lock)
        {
            return _dictionary.Values.Any(predicate);
        }
    }

    /// <summary>
    /// Determines whether all values match the predicate.
    /// </summary>
    public bool All(Func<TValue, bool> predicate)
    {
        lock (_lock)
        {
            return _dictionary.Values.All(predicate);
        }
    }

    /// <summary>
    /// Performs an action on each key-value pair in the dictionary.
    /// </summary>
    /// <remarks>The lock is held for the entire duration of the iteration.</remarks>
    public void ForEach(Action<TKey, TValue> action)
    {
        lock (_lock)
        {
            foreach (var kvp in _dictionary)
            {
                action(kvp.Key, kvp.Value);
            }
        }
    }

    /// <summary>
    /// Creates a shallow copy of the dictionary.
    /// </summary>
    /// <remarks>The returned dictionary is NOT thread-safe.</remarks>
    public Dictionary<TKey, TValue> ToDictionary()
    {
        lock (_lock)
        {
            return new Dictionary<TKey, TValue>(_dictionary);
        }
    }

    /// <summary>
    /// Executes a function while holding the lock, allowing complex operations.
    /// </summary>
    /// <remarks>Use this for multi-step operations that need to be atomic.</remarks>
    public TResult ExecuteLocked<TResult>(Func<Dictionary<TKey, TValue>, TResult> func)
    {
        lock (_lock)
        {
            return func(_dictionary);
        }
    }

    /// <summary>
    /// Executes an action while holding the lock, allowing complex operations.
    /// </summary>
    /// <remarks>Use this for multi-step operations that need to be atomic.</remarks>
    public void ExecuteLocked(Action<Dictionary<TKey, TValue>> action)
    {
        lock (_lock)
        {
            action(_dictionary);
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the dictionary.
    /// </summary>
    /// <remarks>This creates a snapshot of the dictionary to avoid locking during enumeration.</remarks>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        Dictionary<TKey, TValue> snapshot;
        lock (_lock)
        {
            snapshot = new Dictionary<TKey, TValue>(_dictionary);
        }
        return snapshot.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}