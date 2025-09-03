namespace TwitchAPI.shared;

public class FixedSizeDictionary<TKey, TValue> where TKey : notnull {
    private readonly Dictionary<TKey, TValue> _dictionary;
    private readonly Queue<TKey> _keyQueue;
    private readonly int _maxSize;

    public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);
    public int Count => _dictionary.Count;
    
    
    public FixedSizeDictionary(int maxSize) {
        if (maxSize <= 0) { 
            throw new ArgumentException("Max size must be greater than 0");
        }
        
        _maxSize = maxSize;
        _dictionary = new Dictionary<TKey, TValue>();
        _keyQueue = new Queue<TKey>();
    }

    public void Add(TKey key, TValue value) {
        if (_dictionary.ContainsKey(key)) {
            _dictionary[key] = value;
            return;
        }

        if (_dictionary.Count >= _maxSize) {
            var oldestKey = _keyQueue.Dequeue();
            _dictionary.Remove(oldestKey);
        }

        _dictionary.Add(key, value);
        _keyQueue.Enqueue(key);
    }

    public TValue this[TKey key] {
        get => _dictionary[key];
        set => Add(key, value);
    }
}