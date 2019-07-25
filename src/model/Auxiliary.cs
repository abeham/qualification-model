using System;
using System.Collections.Generic;
using System.Linq;
using SimSharp;

namespace model {
  public static class Auxiliary {
    /// <summary>
    /// Selects all elements in the sequence that are minimal with respect to the given value.
    /// </summary>
    /// <remarks>
    /// Runtime complexity of the operation is O(N).
    /// </remarks>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="source">The enumeration in which items with a minimal value should be found.</param>
    /// <param name="valueSelector">The function that selects the value.</param>
    /// <returns>All elements in the enumeration where the selected value is the minimum.</returns>
    public static IEnumerable<T> MinItems<T>(this IEnumerable<T> source, Func<T, IComparable> valueSelector) {
      IEnumerator<T> enumerator = source.GetEnumerator();
      if (!enumerator.MoveNext()) return Enumerable.Empty<T>();
      IComparable min = valueSelector(enumerator.Current);
      var result = new List<T>();
      result.Add(enumerator.Current);

      while (enumerator.MoveNext()) {
        T item = enumerator.Current;
        IComparable comparison = valueSelector(item);
        if (comparison.CompareTo(min) < 0) {
          result.Clear();
          result.Add(item);
          min = comparison;
        } else if (comparison.CompareTo(min) == 0) {
          result.Add(item);
        }
      }
      return result;
    }

    /// <summary>
    /// Chooses one elements from a sequence giving each element an equal chance.
    /// </summary>
    /// <remarks>
    /// Runtime complexity is O(1) for sequences that are of type <see cref="IList{T}"/> and
    /// O(N) for all other.
    /// </remarks>
    /// <exception cref="ArgumentException">If the sequence is empty.</exception>
    /// <typeparam name="T">The type of the items to be selected.</typeparam>
    /// <param name="source">The sequence of elements.</param>
    /// <param name="random">The random number generator to use, its NextDouble() method must produce values in the range [0;1)</param>
    /// <param name="count">The number of items to be selected.</param>
    /// <returns>An element that has been chosen randomly from the sequence.</returns>
    public static T SampleRandom<T>(this IEnumerable<T> source, IRandom random) {
      if (!source.Any()) throw new ArgumentException("sequence is empty.", "source");
      return source.SampleRandom(random, 1).First();
    }

    /// <summary>
    /// Chooses <paramref name="count"/> elements from a sequence with repetition with equal chances for each element.
    /// </summary>
    /// <remarks>
    /// Runtime complexity is O(count) for sequences that are <see cref="IList{T}"/> and
    /// O(N * count) for all other. No exception is thrown if the sequence is empty.
    /// 
    /// The method is online.
    /// </remarks>
    /// <typeparam name="T">The type of the items to be selected.</typeparam>
    /// <param name="source">The sequence of elements.</param>
    /// <param name="random">The random number generator to use, its NextDouble() method must produce values in the range [0;1)</param>
    /// <param name="count">The number of items to be selected.</param>
    /// <returns>A sequence of elements that have been chosen randomly.</returns>
    public static IEnumerable<T> SampleRandom<T>(this IEnumerable<T> source, IRandom random, int count) {
      var listSource = source as IList<T>;
      if (listSource != null) {
        while (count > 0) {
          yield return listSource[random.Next(listSource.Count)];
          count--;
        }
      } else {
        while (count > 0) {
          var enumerator = source.GetEnumerator();
          enumerator.MoveNext();
          T selectedItem = enumerator.Current;
          int counter = 1;
          while (enumerator.MoveNext()) {
            counter++;
            if (counter * random.NextDouble() < 1.0)
              selectedItem = enumerator.Current;
          }
          yield return selectedItem;
          count--;
        }
      }
    }
  }
}
