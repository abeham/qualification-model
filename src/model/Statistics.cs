using System;
using SimSharp;

namespace model {
  public sealed class BasicStatistics {
    public int Count { get; private set; }

    public double Min { get; private set; }
    public double Max { get; private set; }
    public double Total { get; private set; }
    public double Mean { get; private set; }
    public double StdDev { get { return Math.Sqrt(Variance); } }
    public double Variance { get { return (Count > 0) ? variance / Count : 0.0; } }
    private double variance;

    public BasicStatistics() {
    }

    public void Add(double value) {
      Count++;

      if (Count == 1) {
        Min = Max = Mean = value;
      } else {
        if (value < Min) Min = value;
        if (value > Max) Max = value;

        Total += value;
        var oldMean = Mean;
        Mean = oldMean + (value - oldMean) / Count;
        variance = variance + (value - oldMean) * (value - Mean);
      }
    }

    public void Reset() {
      Count = 0;
      Min = Max = Total = Mean = 0;
      variance = 0;
    }
  }

  public sealed class TimeBasedStatistics {
    private readonly Simulation env;

    public int Count { get; private set; }
    public double TotalTimeD { get; private set; }
    public TimeSpan TotalTime { get { return env.ToTimeSpan(TotalTimeD); } }

    public double Min { get; private set; }
    public double Max { get; private set; }
    public double Area { get; private set; }
    public double Mean { get; private set; }
    public double StdDev { get { return Math.Sqrt(Variance); } }
    public double Variance { get { return (TotalTimeD > 0) ? variance / TotalTimeD : 0.0; } }

    private double lastUpdateTime;
    private double lastValue;
    public double Current { get { return lastValue; } }

    private double variance;

    private bool firstSample;

    public void Reset(double initial = 0) {
      Count = 0;
      TotalTimeD = 0;
      Min = Max = Area = Mean = 0;
      variance = 0;
      firstSample = false;
      lastUpdateTime = env.NowD;
      lastValue = initial;
    }

    public TimeBasedStatistics(Simulation env, double initial = 0) {
      this.env = env;
      lastUpdateTime = env.NowD;
      lastValue = initial;
    }

    public void Increase(double value = 1) {
      UpdateTo(lastValue + value);
    }

    public void Decrease(double value = 1) {
      UpdateTo(lastValue - value);
    }

    public void UpdateTo(double value) {
      Count++;

      if (!firstSample) {
        Min = Max = Mean = value;
        firstSample = true;
      } else {
        if (value < Min) Min = value;
        if (value > Max) Max = value;

        var duration = env.NowD - lastUpdateTime;
        if (duration > 0) {
          Area += (lastValue * duration);
          var oldMean = Mean;
          Mean = oldMean + (lastValue - oldMean) * duration / (duration + TotalTimeD);
          variance = variance + (lastValue - oldMean) * (lastValue - Mean) * duration;
          TotalTimeD += duration;
        }
      }

      lastUpdateTime = env.NowD;
      lastValue = value;
    }
  }
}
