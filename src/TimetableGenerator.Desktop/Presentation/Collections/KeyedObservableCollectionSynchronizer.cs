using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace TimetableGenerator.Desktop.Presentation.Collections;

internal static class KeyedObservableCollectionSynchronizer
{
    public static void Synchronize<TItem, TKey>(
        ObservableCollection<TItem> currentItems,
        IReadOnlyList<TItem> desiredItems,
        Func<TItem, TKey> findKey)
        where TKey : notnull
    {
        Debug.Assert(currentItems != null);
        Debug.Assert(desiredItems != null);
        Debug.Assert(findKey != null);

        HashSet<TKey> desiredKeys = new HashSet<TKey>();
        foreach (TItem desiredItem in desiredItems)
        {
            TKey desiredKey = findKey(desiredItem);
            if (desiredKeys.Add(desiredKey) == false)
            {
                throw new InvalidOperationException("The desired collection contains duplicate keys.");
            }
        }

        HashSet<TKey> currentKeys = new HashSet<TKey>();
        foreach (TItem currentItem in currentItems)
        {
            TKey currentKey = findKey(currentItem);
            if (currentKeys.Add(currentKey) == false)
            {
                throw new InvalidOperationException("The current collection contains duplicate keys.");
            }
        }

        removeItemsNotInDesiredCollection(currentItems, desiredKeys, findKey);

        EqualityComparer<TKey> keyComparer = EqualityComparer<TKey>.Default;
        for (int desiredIndex = 0;
            desiredIndex < desiredItems.Count;
            ++desiredIndex)
        {
            TItem desiredItem = desiredItems[desiredIndex];
            TKey desiredKey = findKey(desiredItem);
            if (hasKeyAtIndex(currentItems, desiredIndex, desiredKey, findKey, keyComparer))
            {
                continue;
            }

            int currentIndex = findItemIndexByKey(
                currentItems,
                desiredIndex + 1,
                desiredKey,
                findKey,
                keyComparer);
            if (currentIndex >= 0)
            {
                currentItems.Move(currentIndex, desiredIndex);
                continue;
            }

            currentItems.Insert(desiredIndex, desiredItem);
        }
    }

    private static void removeItemsNotInDesiredCollection<TItem, TKey>(
        ObservableCollection<TItem> currentItems,
        IReadOnlySet<TKey> desiredKeys,
        Func<TItem, TKey> findKey)
        where TKey : notnull
    {
        for (int index = currentItems.Count - 1; index >= 0; --index)
        {
            TKey currentKey = findKey(currentItems[index]);
            if (desiredKeys.Contains(currentKey) == false)
            {
                currentItems.RemoveAt(index);
            }
        }
    }

    private static bool hasKeyAtIndex<TItem, TKey>(
        IReadOnlyList<TItem> currentItems,
        int index,
        TKey expectedKey,
        Func<TItem, TKey> findKey,
        EqualityComparer<TKey> keyComparer)
        where TKey : notnull
    {
        if (index >= currentItems.Count)
        {
            return false;
        }

        TKey currentKey = findKey(currentItems[index]);
        return keyComparer.Equals(currentKey, expectedKey);
    }

    private static int findItemIndexByKey<TItem, TKey>(
        IReadOnlyList<TItem> currentItems,
        int startIndex,
        TKey expectedKey,
        Func<TItem, TKey> findKey,
        EqualityComparer<TKey> keyComparer)
        where TKey : notnull
    {
        for (int index = startIndex; index < currentItems.Count; ++index)
        {
            TKey currentKey = findKey(currentItems[index]);
            if (keyComparer.Equals(currentKey, expectedKey))
            {
                return index;
            }
        }

        return -1;
    }
}
