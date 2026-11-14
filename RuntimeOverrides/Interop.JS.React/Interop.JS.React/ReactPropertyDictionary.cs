using System;
using System.Collections.Generic;

namespace Interop.JS.React;

public sealed class ReactPropertyDictionary
{
    private Entry? _first;
    private Entry? _last;
    private int _count;

    internal ReactPropertyDictionary()
    {
    }

    private ReactPropertyDictionary(ReactPropertyDictionary source)
    {
        var entry = source._first;
        while (entry != null)
        {
            this[entry.Name] = entry.Value;
            entry = entry.Next;
        }
    }

    public int Count => _count;

    public object? this[string name]
    {
        get
        {
            var entry = Find(name);
            if (entry == null)
            {
                throw new KeyNotFoundException($"The React property '{name}' was not present.");
            }
            return entry.Value;
        }
        set
        {
            var entry = Find(name);
            if (entry != null)
            {
                entry.Value = value;
                return;
            }
            var added = new Entry(name, value);
            if (_last == null)
            {
                _first = added;
            }
            else
            {
                _last.Next = added;
            }
            _last = added;
            _count++;
        }
    }

    public bool ContainsKey(string name) => Find(name) != null;

    public bool TryGetValue(string name, out object? value)
    {
        var entry = Find(name);
        if (entry == null)
        {
            value = null;
            return false;
        }
        value = entry.Value;
        return true;
    }

    public bool Remove(string name)
    {
        Entry? previous = null;
        var entry = _first;
        while (entry != null && entry.Name != name)
        {
            previous = entry;
            entry = entry.Next;
        }
        if (entry == null)
        {
            return false;
        }
        if (previous == null)
        {
            _first = entry.Next;
        }
        else
        {
            previous.Next = entry.Next;
        }
        if (_last == entry)
        {
            _last = previous;
        }
        _count--;
        return true;
    }

    public void Clear()
    {
        _first = null;
        _last = null;
        _count = 0;
    }

    internal string[] GetNames()
    {
        var result = new string[_count];
        var entry = _first;
        var resultIndex = 0;
        while (entry != null)
        {
            result[resultIndex++] = entry.Name;
            entry = entry.Next;
        }
        return result;
    }

    internal ReactPropertyDictionary Snapshot() => new ReactPropertyDictionary(this);

    private Entry? Find(string name)
    {
        var entry = _first;
        while (entry != null)
        {
            if (entry.Name == name)
            {
                return entry;
            }
            entry = entry.Next;
        }
        return null;
    }

    private sealed class Entry
    {
        public Entry(string name, object? value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public object? Value { get; set; }
        public Entry? Next { get; set; }
    }
}
