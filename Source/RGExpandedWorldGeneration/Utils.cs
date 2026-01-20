using System.Collections.Generic;
using System.Linq;

namespace RGExpandedWorldGeneration;

public static class Utils
{
    public static bool ContentEquals<TKey, TValue>(this Dictionary<TKey, TValue> dictionary,
        Dictionary<TKey, TValue> otherDictionary)
    {
        var dict1 = dictionary ?? new Dictionary<TKey, TValue>();
        var dict2 = otherDictionary ?? new Dictionary<TKey, TValue>();

        return dict1.Count == dict2.Count &&
               dict1.All(kvp => dict2.TryGetValue(kvp.Key, out var value) &&
                                EqualityComparer<TValue>.Default.Equals(kvp.Value, value));
    }
}