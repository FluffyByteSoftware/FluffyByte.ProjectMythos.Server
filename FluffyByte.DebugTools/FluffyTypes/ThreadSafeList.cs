using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FluffyByte.Tools.FluffyTypes;

/// <summary>
/// Represents a thread-safe, generic list that provides synchronized access to its elements.
/// </summary>
/// <remarks>This class ensures thread safety by using a lock to synchronize access to the underlying list. It
/// supports common list operations such as adding, removing, and querying elements, as well as advanced operations like
/// executing custom actions or functions while holding the lock.  Note that methods returning collections, such as <see
/// cref="ToList"/> and <see cref="ToArray"/>, produce snapshots of the list at the time of the call. These snapshots
/// are not thread-safe.  Enumeration is performed on a snapshot of the list to avoid holding the lock during
/// iteration.</remarks>
/// <typeparam name="T">The type of elements in the list.</typeparam>
public class ThreadSafeList<T> : IEnumerable<T>
{
    private readonly List<T> _list = [];
    private readonly object _lock = new();

    /// <summary>
    /// Gets the number of elements in the list.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _list.Count;
            }
        }
    }

    /// <summary>
    /// Gets or sets the element at the specified index.
    /// </summary>
    public T this[int index]
    {
        get
        {
            lock (_lock)
            {
                return _list[index];
            }
        }
        set
        {
            lock (_lock)
            {
                _list[index] = value;
            }
        }
    }

    /// <summary>
    /// Adds an item to the list.
    /// </summary>
    public void Add(T item)
    {
        lock (_lock)
        {
            _list.Add(item);
        }
    }

    /// <summary>
    /// Adds multiple items to the list.
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        lock (_lock)
        {
            _list.AddRange(items);
        }
    }

    /// <summary>
    /// Removes the first occurrence of a specific item from the list.
    /// </summary>
    /// <returns>True if item was removed; false if not found.</returns>
    public bool Remove(T item)
    {
        lock (_lock)
        {
            return _list.Remove(item);
        }
    }

    /// <summary>
    /// Removes the element at the specified index.
    /// </summary>
    public void RemoveAt(int index)
    {
        lock (_lock)
        {
            _list.RemoveAt(index);
        }
    }

    /// <summary>
    /// Removes all items that match the predicate.
    /// </summary>
    /// <returns>The number of items removed.</returns>
    public int RemoveAll(Predicate<T> match)
    {
        lock (_lock)
        {
            return _list.RemoveAll(match);
        }
    }

    /// <summary>
    /// Removes all elements from the list.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _list.Clear();
        }
    }

    /// <summary>
    /// Determines whether the list contains a specific item.
    /// </summary>
    public bool Contains(T item)
    {
        lock (_lock)
        {
            return _list.Contains(item);
        }
    }

    /// <summary>
    /// Searches for an element that matches the predicate and returns the first occurrence.
    /// </summary>
    public T? Find(Predicate<T> match)
    {
        lock (_lock)
        {
            return _list.Find(match);
        }
    }

    /// <summary>
    /// Retrieves all elements that match the predicate.
    /// </summary>
    public List<T> FindAll(Predicate<T> match)
    {
        lock (_lock)
        {
            return _list.FindAll(match);
        }
    }

    /// <summary>
    /// Determines whether any element matches the predicate.
    /// </summary>
    public bool Any(Func<T, bool> predicate)
    {
        lock (_lock)
        {
            return _list.Any(predicate);
        }
    }

    /// <summary>
    /// Determines whether all elements match the predicate.
    /// </summary>
    public bool All(Func<T, bool> predicate)
    {
        lock (_lock)
        {
            return _list.All(predicate);
        }
    }

    /// <summary>
    /// Returns the first element that matches the predicate, or default if none found.
    /// </summary>
    public T? FirstOrDefault(Func<T, bool> predicate)
    {
        lock (_lock)
        {
            return _list.FirstOrDefault(predicate);
        }
    }

    /// <summary>
    /// Projects each element into a new form using the selector function.
    /// </summary>
    public List<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        lock (_lock)
        {
            return [.. _list.Select(selector)];
        }
    }

    /// <summary>
    /// Filters elements based on a predicate.
    /// </summary>
    public List<T> Where(Func<T, bool> predicate)
    {
        lock (_lock)
        {
            return [.. _list.Where(predicate)];
        }
    }

    /// <summary>
    /// Creates a shallow copy of the list.
    /// </summary>
    /// <remarks>This returns a new list containing references to the same objects.
    /// The returned list is NOT thread-safe.</remarks>
    public List<T> ToList()
    {
        lock (_lock)
        {
            return [.. _list];
        }
    }

    /// <summary>
    /// Converts the list to an array.
    /// </summary>
    public T[] ToArray()
    {
        lock (_lock)
        {
            return [.. _list];
        }
    }

    /// <summary>
    /// Performs an action on each element in the list.
    /// </summary>
    /// <remarks>The lock is held for the entire duration of the iteration.</remarks>
    public void ForEach(Action<T> action)
    {
        lock (_lock)
        {
            _list.ForEach(action);
        }
    }

    /// <summary>
    /// Executes a function while holding the lock, allowing complex operations.
    /// </summary>
    /// <remarks>Use this for multi-step operations that need to be atomic.</remarks>
    public TResult ExecuteLocked<TResult>(Func<List<T>, TResult> func)
    {
        lock (_lock)
        {
            return func(_list);
        }
    }

    /// <summary>
    /// Executes an action while holding the lock, allowing complex operations.
    /// </summary>
    /// <remarks>Use this for multi-step operations that need to be atomic.</remarks>
    public void ExecuteLocked(Action<List<T>> action)
    {
        lock (_lock)
        {
            action(_list);
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the list.
    /// </summary>
    /// <remarks>This creates a snapshot of the list to avoid locking during enumeration.</remarks>
    public IEnumerator<T> GetEnumerator()
    {
        List<T> snapshot;
        lock (_lock)
        {
            snapshot = [.. _list];
        }
        return snapshot.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}