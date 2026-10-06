using System;
using System.Collections.Generic;

namespace Hearthglade.Gameplay.Helpers.Extensions {
    public static class ExtensionMethods {
        
        // Checking if value is between two values inclusive
        public static bool IsBetween<T>(this T item, T start, T end) {
            return Comparer<T>.Default.Compare(item, start) >= 0
                   && Comparer<T>.Default.Compare(item, end) <= 0;
        }
        
        public static T GetRandom<T>(this T[] items) {
            return items[UnityEngine.Random.Range(0, items.Length)];
        }
        
        public static T GetRandom<T>(this List<T> items) {
            return items[UnityEngine.Random.Range(0, items.Count)];
        }

        public static bool NullOrEmpty( this string str) {
            return str == null || str.Length == 0;
        }


        
    }
}