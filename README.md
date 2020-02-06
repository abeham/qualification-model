# Overview

This repository contains an implementation of a simulation model that evaluates the impact of cross-training in a production planning system. The model is implemented using Sim# and describes a production system that consists of two lines, two finished goods per line and 3 stages per line. The model is balanced for 48 workers with 8 workers per stage and line.

## Parameters

You can generate workforces with various skill distributions

    // Generates a workforce where all workers possess all skills
    var fullFlexibleWorkforce = BinaryQualifiedWorkforce.FromVector(Enumerable.Repeat(true, 46 * 6), 6);
    // Generates a workforce where 23 workers possess all skills for line 1 and 23 workers possess all skills for line 2
    var linewiseFlexibleWorkforce = BinaryQualifiedWorkforce.FromVector(Enumerable.Repeat(new[] { true, true, true, false, false, false }, 23).Concat(Enumerable.Repeat(new[] { false, false, false, true, true, true }, 23)).SelectMany(x => x), 6);


## Experiment

The model is implemented using .NET Core 2.2. You can run it using Visual Studio or through the command line:

    dotnet run --project runner
