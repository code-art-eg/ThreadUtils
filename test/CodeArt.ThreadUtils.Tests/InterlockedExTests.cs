using System;
using System.Threading;
using Xunit;

namespace CodeArt.ThreadUtils.Tests
{
    public class InterlockedExTests
    {
        [Fact]
        public void TestIntApply()
        {
            int val = 0;
            int iterations = 100_000;
            int cpuCount = Environment.ProcessorCount;
            Thread[] threads = new Thread[cpuCount * 2];

            static int Fn(int v)
            {
                return v + 1;
            }
            
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    for (var j = 0; j < iterations; j++)
                    {
                        InterlockedEx.Apply(ref val, Fn);
                    }
                });
                threads[i].Start();
            }

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }
            Assert.Equal(iterations * threads.Length, val);
        }

        [Fact]
        public void TestLongApply()
        {
            long val = 0;
            int iterations = 100_000;
            int cpuCount = Environment.ProcessorCount;
            Thread[] threads = new Thread[cpuCount * 2];

            static long Fn(long v)
            {
                return v + 1;
            }

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    for (var j = 0; j < iterations; j++)
                    {
                        InterlockedEx.Apply(ref val, Fn);
                    }
                });
                threads[i].Start();
            }

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }
            Assert.Equal(iterations * threads.Length, val);
        }
    }
}
