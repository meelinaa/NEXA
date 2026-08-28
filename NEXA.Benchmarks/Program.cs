using System;
using BenchmarkDotNet.Running;

namespace NEXA.Benchmarks;

/// <summary>
/// Benchmark runner entry point for the NEXA high-performance computer vision pipeline micro-benchmarks.
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("===============================================================");
        Console.WriteLine("    NEXA Engine - Micro-Benchmark & Performance Suite          ");
        Console.WriteLine("===============================================================");

        BenchmarkSwitcher switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);

        if (args.Length == 0)
        {
            // By default, run all benchmarks or display switcher menu
            switcher.RunAll();
        }
        else
        {
            switcher.Run(args);
        }
    }
}
