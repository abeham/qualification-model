using System;
using System.Collections.Generic;
using System.Linq;
using SimSharp;

namespace model {
  public enum DispatchStrategy { FirstComeFirstServe, LeastSkillFirst, ModifiedLeastSkillFirst }

  public class SimulationModel {
    // Configuration
    public readonly double UtilizationTarget;
    public readonly double OrderAmount;
    public readonly double ProcessingRatioWorker;
    public readonly double ChangeTimeRatio;
    public readonly double LineChangeFactor;
    public readonly double DueDateHorizonFix;
    public readonly double DueDateHorizonVar;
    public readonly double CV_DueDate;
    public readonly double CV_ProcessingTime;
    public readonly double CV_Interarrival;
    public readonly double ObservationTime;
    public readonly double WarmupTime;

    private readonly IWorkforce _workforce;
    private readonly DispatchStrategy _dispatch;

    // System Configuration
    public readonly double ProcessingTimeStations; // equal for all stations Si
    private readonly int[] _capacity;
    public IReadOnlyList<int> Capacity {
      get { return _capacity; }
    }

    /*                   S2 => Product 1
     * Line 1: S0 - S1 < 
     *                   S3 => Product 2
     * 
     *                   S6 => Product 3
     * Line 2: S4 - S5 < 
     *                   S7 => Product 4
     */

    // Constants
    public readonly double InterArrivalMean;
    private readonly double[,] _changeTimeMean;
    private readonly int[] _qualificationByStation;

    // Dynamic entities
    private readonly IRandom _randDemand;
    private readonly IRandom _randDueDate;
    private readonly IRandom _randProc;
    private readonly IRandom _randWorkerTieBreak;
    private readonly Simulation _env;
    private readonly ResourcePool _pool;
    private readonly Resource[] _stations;
    private readonly List<List<int>> _workersByQualification, _qualificationByWorker;
    private readonly int[] _lastStationByWorker;
    private readonly int[] _activeStations;
    private int _activeWorkers;

    public TimeBasedStatistics[] Backlog { get; }
    public TimeBasedStatistics SystemUtilization { get; }
    public TimeBasedStatistics[] StationUtilization { get; }
    public TimeBasedStatistics WIPInventory { get; }
    public TimeBasedStatistics FGIInventory { get; }
    public TimeBasedStatistics WorkerUtilization { get; }
    public TimeBasedStatistics[] WorkerUtilizations { get; }
    public TimeBasedStatistics Backorders { get; }
    public BasicStatistics WIPLeadTime { get; }
    public BasicStatistics FGILeadTime { get; }
    public BasicStatistics Tardiness { get; }
    public BasicStatistics ServiceLevel { get; }

    private Dictionary<Process, Tuple<double, double>> _backorderedJobs;

    public SimulationModel(double util, double orderAmount, double pr, double cr, double lcf,
      double dueDateFix, double dueDateVar, double dueDateCV, double procTimeCV, double interarrivalCV,
      IWorkforce workforce, DispatchStrategy dispatch, int rseed = 0, double observationTime = 3600,
      double warmupTime = 600) {
      _randDemand = new PcgRandom(rseed);
      _randDueDate = new PcgRandom(rseed + 1);
      _randProc = new PcgRandom(rseed + 2);
      _randWorkerTieBreak = new PcgRandom(rseed + 3);
      UtilizationTarget = util;
      OrderAmount = orderAmount;
      ProcessingRatioWorker = pr;
      ChangeTimeRatio = cr;
      LineChangeFactor = lcf;
      DueDateHorizonFix = dueDateFix;
      DueDateHorizonVar = dueDateVar;
      CV_DueDate = dueDateCV;
      CV_ProcessingTime = procTimeCV;
      CV_Interarrival = interarrivalCV;
      ObservationTime = observationTime;
      WarmupTime = warmupTime;

      ProcessingTimeStations = 1;

      _capacity = new[] { 8, 8, 4, 4, 8, 8, 4, 4 }.Select(c => (int)Math.Round(c / pr)).ToArray();

      // 2 products per independent line
      InterArrivalMean = (2 * OrderAmount * ProcessingTimeStations) / (UtilizationTarget * _capacity[0]);
      var ct = ChangeTimeRatio * ProcessingRatioWorker * ProcessingTimeStations;
      var lct = ct * LineChangeFactor;

      _changeTimeMean = new double[,] {
        {   0,  ct,  ct,  ct, lct, lct, lct, lct },
        {  ct,   0,  ct,  ct, lct, lct, lct, lct },
        {  ct,  ct,   0,   0, lct, lct, lct, lct },
        {  ct,  ct,   0,   0, lct, lct, lct, lct },
        { lct, lct, lct, lct,   0,  ct,  ct,  ct },
        { lct, lct, lct, lct,  ct,   0,  ct,  ct },
        { lct, lct, lct, lct,  ct,  ct,   0,   0 },
        { lct, lct, lct, lct,  ct,  ct,   0,   0 }
      };

      _qualificationByStation = new[] { 0, 1, 2, 2, 3, 4, 5, 5 };

      _workforce = workforce;
      _dispatch = dispatch;

      _env = new Simulation(rseed);
      _pool = new ResourcePool(_env, Enumerable.Range(0, _workforce.Workers).Cast<object>());
      _workersByQualification = _workforce.GetWorkersByQualification().ToList();
      _qualificationByWorker = _workforce.GetQualificationByWorker().ToList();
      _stations = _capacity.Select(m => new Resource(_env, Math.Max(m, 1))).ToArray();
      _lastStationByWorker = Enumerable.Repeat(-1, _workforce.Workers).ToArray();
      _activeStations = new int[_capacity.Length];
      _activeWorkers = 0;

      Backlog = _stations.Select(t => new TimeBasedStatistics(_env)).ToArray();
      SystemUtilization = new TimeBasedStatistics(_env);
      StationUtilization = _stations.Select(t => new TimeBasedStatistics(_env)).ToArray();
      WIPInventory = new TimeBasedStatistics(_env);
      FGIInventory = new TimeBasedStatistics(_env);
      WorkerUtilization = new TimeBasedStatistics(_env);
      WorkerUtilizations = Enumerable.Range(0, _workforce.Workers).Select(x => new TimeBasedStatistics(_env)).ToArray();
      Backorders = new TimeBasedStatistics(_env);
      WIPLeadTime = new BasicStatistics();
      FGILeadTime = new BasicStatistics();
      Tardiness = new BasicStatistics();
      ServiceLevel = new BasicStatistics();

      _backorderedJobs = new Dictionary<Process, Tuple<double, double>>();
    }

    public void Run() {
      _env.Process(EndWarmupPhase(WarmupTime));
      _env.Process(Demand(route: new int[] { 0, 1, 2 }));
      _env.Process(Demand(route: new int[] { 0, 1, 3 }));
      _env.Process(Demand(route: new int[] { 4, 5, 6 }));
      _env.Process(Demand(route: new int[] { 4, 5, 7 }));
      _env.RunD(WarmupTime + ObservationTime);

      foreach (var job in _backorderedJobs) {
        ServiceLevel.Add(0);
        WIPLeadTime.Add(_env.NowD - job.Value.Item1);
        Tardiness.Add(_env.NowD - job.Value.Item2);
      }
    }

    private IEnumerable<Event> EndWarmupPhase(double time) {
      yield return _env.TimeoutD(time);
      for (var b = 0; b < Backlog.Length; b++)
        Backlog[b].Reset(Backlog[b].Current);
      SystemUtilization.Reset(SystemUtilization.Current);
      for (var u = 0; u < StationUtilization.Length; u++)
        StationUtilization[u].Reset(StationUtilization[u].Current);
      WIPInventory.Reset(WIPInventory.Current);
      FGIInventory.Reset(FGIInventory.Current);
      WorkerUtilization.Reset(WorkerUtilization.Current);
      for (var w = 0; w < WorkerUtilizations.Length; w++)
        WorkerUtilizations[w].Reset(WorkerUtilizations[w].Current);
      Backorders.Reset(Backorders.Current);
      WIPLeadTime.Reset();
      FGILeadTime.Reset();
      Tardiness.Reset();
      ServiceLevel.Reset();
    }

    protected static readonly double NormalMagicConst = 4 * Math.Exp(-0.5) / Math.Sqrt(2.0);
    private static double GetNormal(IRandom rand, double mu, double sigma) {
      double z, zz, u1, u2;
      do {
        u1 = rand.NextDouble();
        u2 = 1 - rand.NextDouble();
        z = NormalMagicConst * (u1 - 0.5) / u2;
        zz = z * z / 4.0;
      } while (zz > -Math.Log(u2));
      return mu + z * sigma;
    }

    private static double GetLogNormal(IRandom rand, double mu, double sigma) {
      if (sigma == 0) return mu;
      var alpha = Math.Sqrt(mu * sigma) / mu;
      var sigmaln = Math.Sqrt(Math.Log(1 + (alpha * alpha)));
      var muln = Math.Log(mu) - 0.5 * sigmaln * sigmaln;
      return Math.Exp(GetNormal(rand, muln, sigmaln));
    }

    private IEnumerable<Event> Demand(int[] route) {
      while (true) {
        yield return _env.TimeoutD(GetLogNormal(_randDemand, InterArrivalMean, CV_Interarrival));
        //var due = _env.NowD + DueDateHorizonFix + _env.RandExponential(DueDateHorizonVar);
        var due = _env.NowD + DueDateHorizonFix + GetLogNormal(_randDueDate, DueDateHorizonVar, CV_DueDate);
        _env.Process(Job(route, due));
      }
    }

    private IEnumerable<Event> Job(int[] route, double due) {
      var start = _env.NowD;
      var flow = _env.Process(JobFlow(route)); // wait until job is finished
      yield return flow | _env.TimeoutD(due - start);
      if (flow.IsAlive) {
        Backorders.Increase();
        _backorderedJobs.Add(flow, Tuple.Create(start, due));
        yield return flow;
        _backorderedJobs.Remove(flow);
      }
      WIPLeadTime.Add(_env.NowD - start);
      var tardiness = Math.Max(_env.NowD - due, 0);
      Tardiness.Add(tardiness);
      if (_env.NowD < due) {
        ServiceLevel.Add(1);
        FGIInventory.Increase();
        var fgiDelay = due - _env.NowD;
        yield return _env.TimeoutD(fgiDelay); // wait until due date to deliver order
        FGIInventory.Decrease();
        FGILeadTime.Add(fgiDelay);
      } else {
        ServiceLevel.Add(0);
        FGILeadTime.Add(0);
        Backorders.Decrease();
      }
    }

    private IEnumerable<Event> JobFlow(int[] route) {
      WIPInventory.Increase();
      for (var step = 0; step < route.Length; step++) {
        var station = route[step];
        Backlog[station].Increase();
        var s = _stations[station].Request();
        yield return s; // wait until next station in route is available

        var req = GetWorker(station);
        if (req == null) throw new InvalidOperationException($"There is no worker qualified to work at station {station}");
        yield return req; // wait until worker request

        var worker = (int)req.Value;
        var lastStation = _lastStationByWorker[worker];
        if (lastStation >= 0 && _changeTimeMean[lastStation, station] > 0) {
          yield return _env.TimeoutD(_changeTimeMean[lastStation, station]); // wait until worker has changed station
        }
        _lastStationByWorker[worker] = station;
        _activeStations[station]++;
        _activeWorkers++;

        SystemUtilization.UpdateTo(_activeStations.Sum() / (double)_capacity.Sum());
        StationUtilization[station].UpdateTo(_activeStations[station] / (double)_capacity[station]);
        Backlog[station].Decrease();
        WorkerUtilization.UpdateTo(_activeWorkers / (double)_workforce.Workers);
        WorkerUtilizations[worker].UpdateTo(1);

        //var procTime = _env.RandExponential(_processTimes * _orderAmount);
        var procTime = GetLogNormal(_randProc, ProcessingTimeStations * OrderAmount, CV_ProcessingTime);
        var workerTime = procTime * ProcessingRatioWorker;
        var machineTime = procTime - workerTime;

        yield return _env.TimeoutD(workerTime); // wait until worker has finished set-up at station
        yield return _pool.Release(req); // release the worker
        _activeWorkers--;

        WorkerUtilization.UpdateTo(_activeWorkers / (double)_workforce.Workers);
        WorkerUtilizations[worker].UpdateTo(0);

        yield return _env.TimeoutD(machineTime); // wait until station has finished processing
        yield return _stations[station].Release(s); // release station

        _activeStations[station]--;

        SystemUtilization.UpdateTo(_activeStations.Sum() / (double)_capacity.Sum());
        StationUtilization[station].UpdateTo(_activeStations[station] / (double)_capacity[station]);
      }
      WIPInventory.Decrease();
    }

    private ResourcePoolRequest GetWorker(int station) {
      int qualification = _qualificationByStation[station];
      if (_workersByQualification[qualification].Count == 0) return null; // no worker qualified

      switch (_dispatch) {
        case DispatchStrategy.FirstComeFirstServe:
          return _pool.Request(worker => _workforce.IsQualified((int)worker, qualification));
        case DispatchStrategy.LeastSkillFirst: {
            var avail = _workersByQualification[qualification].Where(w => _pool.IsAvailable(worker => (int)worker == w)).ToList();

            if (avail.Count == 0) {
              return _pool.Request(worker => _workforce.IsQualified((int)worker, qualification));
            } else {
              var chosen = avail.MinItems(w => _qualificationByWorker[w].Count).SampleRandom(_randWorkerTieBreak);
              return _pool.Request(worker => (int)worker == chosen);
            }
          }
        case DispatchStrategy.ModifiedLeastSkillFirst: {
            var avail = _workersByQualification[qualification].Where(w => _pool.IsAvailable(worker => (int)worker == w)).ToList();
            if (avail.Count == 0) { // no worker available, use next one that is qualified
              return _pool.Request(worker => _workforce.IsQualified((int)worker, qualification));
            }
            // check if there is one without change time
            var noChangeTime = avail.Where(w => _lastStationByWorker[w] < 0 || _changeTimeMean[_lastStationByWorker[w], station] == 0).ToList();
            if (noChangeTime.Count > 0) {
              var c1 = noChangeTime.MinItems(x => _qualificationByWorker[x].Count).SampleRandom(_randWorkerTieBreak);
              return _pool.Request(worker => (int)worker == c1);
            }
            // otherwise use least skill first
            var c2 = avail.MinItems(w => _qualificationByWorker[w].Count).SampleRandom(_randWorkerTieBreak);
            return _pool.Request(worker => (int)worker == c2);
          }
        default: throw new NotImplementedException($"Dispatching strategy {_dispatch} not implemented.");
      }
    }
  }
}
