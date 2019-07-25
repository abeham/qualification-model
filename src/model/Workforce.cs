using System;
using System.Collections.Generic;
using System.Linq;

namespace model {
  public interface IWorkforce {
    int Qualifications { get; }
    int Workers { get; }

    bool IsQualified(int worker, int qualification);
    double GetQualificationLevel(int worker, int qualification);
    IEnumerable<List<int>> GetQualificationByWorker();
    IEnumerable<List<int>> GetWorkersByQualification();

    int GetTotalQualifications();
  }

  public class BinaryQualifiedWorkforce : IWorkforce {
    private bool[] _enc;

    public int Qualifications { get; }
    public int Workers { get { return _enc.Length / Qualifications; } }

    private BinaryQualifiedWorkforce(IEnumerable<bool> vec, int tasks) {
      _enc = vec.ToArray();
      if (_enc.Length % tasks > 0) throw new ArgumentException("The number of workers is not an integer");
      Qualifications = tasks;
    }

    /// <summary>
    /// Creates a new binary qualification matrix object.
    /// </summary>
    /// <param name="vec">A vector of qualifications as a result of concatenating the qualifications of each worker</param>
    /// <param name="qualifications">The number of tasks to know when a new worker qualification starts</param>
    /// <returns>The qualification matrix</returns>
    public static BinaryQualifiedWorkforce FromVector(IEnumerable<bool> vec, int qualifications) {
      return new BinaryQualifiedWorkforce(vec, qualifications);
    }

    /// <summary>
    /// Creates a new binary qualification matrix object.
    /// </summary>
    /// <param name="matrix">Rows contain workers, columns contain tasks</param>
    /// <returns>The qualification matrix</returns>
    public static BinaryQualifiedWorkforce FromMatrix(bool[,] matrix) {
      var workers = matrix.GetLength(0);
      var qualifications = matrix.GetLength(1);
      return new BinaryQualifiedWorkforce(Enumerable.Range(0, workers * qualifications).Select(i => matrix[i / qualifications, i % qualifications]), qualifications);
    }

    public bool IsQualified(int worker, int qualification) {
      var idx = worker * Qualifications + qualification;
      if (idx >= _enc.Length) throw new IndexOutOfRangeException("Index " + idx + " is longer than " + _enc.Length);
      return _enc[idx];
    }

    public double GetQualificationLevel(int worker, int qualification) {
      var idx = worker * Qualifications + qualification;
      if (idx >= _enc.Length) throw new IndexOutOfRangeException("Index " + idx + " is longer than " + _enc.Length);
      return _enc[idx] ? 1 : 0;
    }

    public IEnumerable<List<int>> GetQualificationByWorker() {
      for (var w = 0; w < Workers; w++) {
        var tasksList = new List<int>();
        for (var q = 0; q < Qualifications; q++) {
          var idx = w * Qualifications + q;
          if (_enc[idx]) tasksList.Add(q);
        }
        yield return tasksList;
      }
    }

    public IEnumerable<List<int>> GetWorkersByQualification() {
      for (var q = 0; q < Qualifications; q++) {
        var workersList = new List<int>();
        for (var w = 0; w < Workers; w++) {
          var idx = w * Qualifications + q;
          if (_enc[idx]) workersList.Add(w);
        }
        yield return workersList;
      }
    }

    public int GetTotalQualifications() {
      return _enc.Count(x => x);
    }
  }
}
