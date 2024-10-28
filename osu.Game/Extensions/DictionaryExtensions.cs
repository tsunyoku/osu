// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Extensions
{
    public static class DictionaryExtensions
    {
        public static T2 GetValueOrThrow<T1, T2>(this IReadOnlyDictionary<T1, T2> dictionary, T1 key)
            where T1 : notnull
        {
            if (!dictionary.TryGetValue(key, out T2? value))
            {
                throw new DictionaryKeyMissingException(key);
            }

            return value;
        }
    }

    public class DictionaryKeyMissingException : Exception
    {
        public object Key { get; }

        public DictionaryKeyMissingException(object key)
        {
            Key = key;
        }
    }
}
