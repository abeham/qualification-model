using System;
using System.Linq;
using model;

namespace runner {
  class Program {
    static void Main(string[] args) {
      var fullFlexibleWorkforce = BinaryQualifiedWorkforce.FromVector(Enumerable.Repeat(true, 46 * 6), 6);
      var simulation = new SimulationModel(0.95, 5, 0.5, 0.1, 2, 1, 100, 0, 0.25, 1, fullFlexibleWorkforce, DispatchStrategy.FirstComeFirstServe);
      Console.WriteLine("=== Running Simulation ===");
      simulation.Run();
      Console.WriteLine("Service Level: {0:F2}%", simulation.ServiceLevel.Mean * 100);
      Console.WriteLine("Number of Qualifications: {0}", fullFlexibleWorkforce.GetTotalQualifications());
      Console.WriteLine();
      Console.WriteLine("Press enter to exit.");
      Console.ReadLine();
    }
  }
}
