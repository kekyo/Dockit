/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dockit.Internal;

internal static class Utilities
{
#if NET45
    private static class EmptyHolder<T>
    {
        public static readonly T[] Empty = new T[0];
    }

    public static T[] Empty<T>() =>
        EmptyHolder<T>.Empty;
#else
    public static T[] Empty<T>() =>
        Array.Empty<T>();
#endif

    public static IEnumerable<U> Collect<T, U>(
        this IEnumerable<T> enumerable,
        Func<T, U?> selector)
        where U : class
    {
        foreach (var item in enumerable)
        {
            if (selector(item) is { } value)
            {
                yield return value;
            }
        }
    }

    public static IEnumerable<T> LastSkipWhile<T>(
        this IEnumerable<T> enumerable,
        Func<T, bool> predicate)
    {
        var q = new Queue<T>();
        foreach (var item in enumerable)
        {
            if (predicate(item))
            {
                q.Enqueue(item);
            }
            else
            {
                while (q.Count >= 1)
                {
                    yield return q.Dequeue();
                }
                yield return item;
            }
        }
    }

#if !NET6_0_OR_GREATER
    private sealed class DistinctComparer<T, U> : IEqualityComparer<T>
    {
        private readonly Func<T, U> selector;

        public DistinctComparer(Func<T, U> selector) =>
            this.selector = selector;

        public bool Equals(T? x, T? y) =>
            x is { } && y is { } &&
            this.selector(x)!.Equals(this.selector(y));

        public int GetHashCode(T? obj) =>
            this.selector(obj!)!.GetHashCode();
    }

    public static IEnumerable<T> DistinctBy<T, U>(
        this IEnumerable<T> enumerable,
        Func<T, U> selector) =>
        enumerable.Distinct(new DistinctComparer<T, U>(selector));
#endif

    public static string GetDirectoryPath(string path) =>
        Path.GetDirectoryName(path) is { } d ?
            Path.GetFullPath(string.IsNullOrWhiteSpace(d) ? "." : d) :
            Path.DirectorySeparatorChar.ToString();
}
