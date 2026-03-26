using BenchmarkDotNet.Running;
using FMC.Benchmarks;

var summary = BenchmarkRunner.Run<FinanceBenchmarks>();
