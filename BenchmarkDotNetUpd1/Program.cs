using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Alea.Parallel;
using ILGPU;
using ILGPU.Runtime;
using System.Collections.Concurrent;

namespace BenchmarkDotNetUpd1
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<MyBench>(); //to instal Benchmark run in Nuget Console : 'Install-Package BenchmarkDotNet'
            Console.WriteLine(summary);
            Console.ReadLine();
            //Console.WriteLine("Mean time in ???: " + summary.Reports[0].ResultStatistics.Mean);
        }
    }

    #region Test

    public class MyBench
    {
        private static int[] _testingData = new int[10_000_000];
        //[Params(100, 1_000)]
        public static int AmountOfRepetitions { get; set; } = 10_000_000;

        [GlobalSetup]
        public void GlobalSetup()
        {
            FilleOutArrayWithRandomNumbers();
        }

        [Benchmark]
        public int SimpliestSwapMinAndMax()
        {
            int minValue = FindMinValue();
            int maxValue = FindMaxValue();
            SwapMinAndMaxValues(minValue, maxValue);
            return minValue + maxValue;
        }

        [Benchmark]
        public int SimpleParallel()
        {
            var minValueTask = Task.Run(() => FindMinValue());
            var maxValueTask = Task.Run(() => FindMaxValue());
            int minValue = minValueTask.Result;
            int maxValue = maxValueTask.Result;
            SwapMinAndMaxValues(minValue, maxValue);
            return minValue + maxValue;
        }

        [Benchmark]
        public void FullyParallel()
        {
            var minValueTask = Task.Run(() => FindMinValueParallel());
            var maxValueTask = Task.Run(() => FindMaxValueParallel());
            int minValueIndex = minValueTask.Result;
            int maxValueIndex = maxValueTask.Result;
            SwapMinAndMaxValues(minValueIndex, maxValueIndex);
        }

        [Benchmark]
        public int Linq()
        {
            int minValue = _testingData.Min();
            int maxValue = _testingData.Max();
            SwapMinAndMaxValues(minValue, maxValue);
            return minValue + maxValue;
        }

        [Benchmark]
        public int PLinq()
        {
            int minValue = _testingData.AsParallel().Min();
            int maxValue = _testingData.AsParallel().Max();
            SwapMinAndMaxValues(minValue, maxValue);
            return minValue + maxValue;
        }

        [Benchmark]
        public void SIMD()
        {
            int minValue = 0;
            int maxValue = 0;
            SIMDMinMax(_testingData, out minValue, out maxValue);
            SwapMinAndMaxValues(minValue, maxValue);
        }

        [Benchmark]
        public int ParallelSIMD()
        {
            var minResults = new List<int>();
            var maxResults = new List<int>();
            var amountOfCpus = Environment.ProcessorCount;
            var amountOfNumbersFor1Thread = _testingData.Count() / amountOfCpus;
            Span<int> arraySpan = _testingData;
            for (int i = 0; i < _testingData.Count(); i += amountOfNumbersFor1Thread)
            {
                Span<int> arrayPart = arraySpan.Slice(i, amountOfNumbersFor1Thread);
                int minValue = 0;
                int maxValue = 0;
                SIMDMinMax(arrayPart.ToArray(), out minValue, out maxValue);
                minResults.Add(minValue);
                maxResults.Add(maxValue);
            }

            SwapMinAndMaxValues(FindMinValue(minResults), FindMaxValue(maxResults));

            return minResults.Count + maxResults.Count;

            int FindMinValue(List<int> input)
            {
                int currentlyMinValue = int.MaxValue;
                for (int i = 0; i < input.Count(); i++)
                {
                    if (input.ElementAt(i) < currentlyMinValue)
                    {
                        currentlyMinValue = input.ElementAt(i);
                    }
                }
                return currentlyMinValue;
            }

            int FindMaxValue(List<int> input)
            {
                int currentlyMaxValue = int.MinValue;
                for (int i = 0; i < input.Count(); i++)
                {
                    if (input.ElementAt(i) > currentlyMaxValue)
                    {
                        currentlyMaxValue = input.ElementAt(i);
                    }
                }
                return currentlyMaxValue;
            }
        }

        //[Benchmark]
        //public void ILGpuVersionSwapMinAndMax()
        //{
        //    //var minValueTask = Task.Run(() => FindMinValueIndex());
        //    //var maxValueTask = Task.Run(() => FindMaxValueIndex());
        //    //int minValueIndex = minValueTask.Result;
        //    //int maxValueIndex = maxValueTask.Result;
        //    //SwapMinAndMaxValues(minValueIndex, maxValueIndex);
        //}

        private static void SwapMinAndMaxValues(int minValue, int maxValue)
        {
            var temp = _testingData[Array.IndexOf(_testingData, minValue)];
            _testingData[Array.IndexOf(_testingData, minValue)] = _testingData[Array.IndexOf(_testingData, maxValue)];
            _testingData[Array.IndexOf(_testingData, maxValue)] = temp;
        }

        private static int FindMinValue()
        {
            int currentlyMinValue = int.MaxValue;
            int currentlyMinValueIndex = 0;
            for (int i = 0; i < _testingData.Count(); i++)
            {
                if (_testingData.ElementAt(i) < currentlyMinValue)
                {
                    currentlyMinValue = _testingData.ElementAt(i);
                    currentlyMinValueIndex = Array.IndexOf(_testingData, i);
                }
            }
            return currentlyMinValue;
        }

        private static int FindMaxValue()
        {
            int currentlyMaxValue = int.MinValue;
            int currentlyMaxValueIndex = 0;
            for (int i = 0; i < _testingData.Count(); i++)
            {
                if (_testingData.ElementAt(i) > currentlyMaxValue)
                {
                    currentlyMaxValue = _testingData.ElementAt(i);
                    currentlyMaxValueIndex = Array.IndexOf(_testingData, i);
                }
            }
            return currentlyMaxValue;
        }

        static object minLocker = new object();
        private static int FindMinValueParallel()
        {
            int currentlyMinValue = int.MaxValue;
            Parallel.For(0, _testingData.Count(), (i) => {
                if (_testingData.ElementAt(i) < currentlyMinValue)
                {
                    lock (minLocker)
                    {
                        currentlyMinValue = _testingData.ElementAt(i);
                    }
                }
            });

            return currentlyMinValue;
        }

        static object maxLocker = new object();
        private static int FindMaxValueParallel()
        {
            int currentlyMaxValue = int.MinValue;
            Parallel.For(0, _testingData.Count(), (i) =>
                {
                    if (_testingData.ElementAt(i) > currentlyMaxValue)
                    {
                        lock (maxLocker)
                        {
                            currentlyMaxValue = _testingData.ElementAt(i);
                        }
                    }
                });
            return currentlyMaxValue;
        }

        private static void FilleOutArrayWithRandomNumbers()
        {
            var rand = new Random();
            Parallel.For(0, AmountOfRepetitions, (i) =>
            {
                _testingData[i] = rand.Next(int.MinValue, int.MaxValue);
            });
        }

        public static void SIMDMinMax(int[] input, out int min, out int max)
        {
            var simdLength = Vector<int>.Count;
            var vmin = new Vector<int>(int.MaxValue);
            var vmax = new Vector<int>(int.MinValue);
            var i = 0;

            // Find the max and min for each of Vector<int>.Count sub-arrays 
            for (i = 0; i <= input.Length - simdLength; i += simdLength)
            {
                var va = new Vector<int>(input, i);
                vmin = Vector.Min(va, vmin);
                vmax = Vector.Max(va, vmax);
            }

            // Find the max and min of all sub-arrays
            min = int.MaxValue;
            max = int.MinValue;
            for (var j = 0; j < simdLength; ++j)
            {
                min = Math.Min(min, vmin[j]);
                max = Math.Max(max, vmax[j]);
            }

            // Process any remaining elements
            for (; i < input.Length; ++i)
            {
                min = Math.Min(min, input[i]);
                max = Math.Max(max, input[i]);
            }
        }
    }




    #endregion

}
