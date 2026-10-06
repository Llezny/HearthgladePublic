using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthglade.Gameplay.Helpers
{
    public class RandomNumberGenerator<T> {
        private List<Tuple<double, T>> items = new List<Tuple<double, T>>();
        private Random random = new Random();
        private double totalPossibility = 0;

        public RandomNumberGenerator() {}

        public RandomNumberGenerator( List<Tuple<double, T>> items ) {
            this.items = items;
        }

        public void Add(double possibility, T item)
        {
            items.Add(new Tuple<double, T>(possibility, item));
            totalPossibility += possibility;
        }

        public T NextItem()
        {
            var rand = random.NextDouble() * totalPossibility;
            double value = 0;
            foreach (var item in items)
            {
                value += item.Item1;
                if (rand <= value)
                    return item.Item2;
            }
            return items.Last().Item2; // Should never happen
        }
    }
}