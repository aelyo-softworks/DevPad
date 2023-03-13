using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DevPad.Utilities
{
    public sealed class ConcurrentHashSet<T> : ICollection<T>, IEnumerable<T>, IEnumerable, ISet<T>, IReadOnlyCollection<T>
    {
        private readonly ConcurrentDictionary<T, long> _dic;

        public ConcurrentHashSet(IEqualityComparer<T> comparer = null)
        {
            _dic = new ConcurrentDictionary<T, long>(comparer);
        }

        public bool Add(T item)
        {
            var added = true;
            _dic.AddOrUpdate(item, 0, (k, old) =>
            {
                added = false;
                return old;
            });
            return added;
        }

        public int Count => _dic.Count;
        public IEnumerator<T> GetEnumerator() => _dic.Keys.GetEnumerator();
        public void Clear() => _dic.Clear();
        public bool Remove(T item) => item != null && _dic.TryRemove(item, out _);
        public bool Contains(T item) => item != null && _dic.ContainsKey(item);

        public void CopyTo(T[] array, int arrayIndex) => throw new NotImplementedException();
        public void ExceptWith(IEnumerable<T> other) => throw new NotImplementedException();
        public void IntersectWith(IEnumerable<T> other) => throw new NotImplementedException();
        public bool IsProperSubsetOf(IEnumerable<T> other) => throw new NotImplementedException();
        public bool IsProperSupersetOf(IEnumerable<T> other) => throw new NotImplementedException();
        public bool IsSubsetOf(IEnumerable<T> other) => throw new NotImplementedException();
        public bool IsSupersetOf(IEnumerable<T> other) => throw new NotImplementedException();
        public bool Overlaps(IEnumerable<T> other) => throw new NotImplementedException();
        public bool SetEquals(IEnumerable<T> other) => throw new NotImplementedException();
        public void SymmetricExceptWith(IEnumerable<T> other) => throw new NotImplementedException();
        public void UnionWith(IEnumerable<T> other) => throw new NotImplementedException();

        void ICollection<T>.Add(T item) => Add(item);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        bool ICollection<T>.IsReadOnly => false;
    }
}
