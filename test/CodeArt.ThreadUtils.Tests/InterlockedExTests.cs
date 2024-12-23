﻿namespace CodeArt.ThreadUtils.Tests;

public class InterlockedExTests
{
    [Fact]
    public void TestIntApply()
    {
        var val = 0;
        const int iterations = 100_000;
        var cpuCount = Environment.ProcessorCount;
        var threads = new Thread[cpuCount * 2];

        for (var i = 0; i < threads.Length; i++)
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

        foreach (var thread in threads)
        {
            thread.Join();
        }
        Assert.Equal(iterations * threads.Length, val);
        return;

        static int Fn(int v)
        {
            return v + 1;
        }
    }

    [Fact]
    public void TestIntApplyReturnValue()
    {
        const int initial = 0;
        var val = initial;

        var res = InterlockedEx.Apply(ref val, Fn);
        Assert.Equal(Fn(initial), res);
        Assert.Equal(res, val);
        return;

        static int Fn(int v)
        {
            return v + 1;
        }
    }

    [Fact]
    public void TestLongApply()
    {
        long val = 0;
        const int iterations = 100_000;
        var cpuCount = Environment.ProcessorCount;
        var threads = new Thread[cpuCount * 2];

        for (var i = 0; i < threads.Length; i++)
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

        foreach (var thread in threads)
        {
            thread.Join();
        }
        Assert.Equal(iterations * threads.Length, val);
        return;

        static long Fn(long v)
        {
            return v + 1;
        }
    }

    [Fact]
    public void TestLongApplyReturnValue()
    {
        const long initial = 0;
        var val = initial;

        var res = InterlockedEx.Apply(ref val, Fn);
        Assert.Equal(Fn(initial), res);
        Assert.Equal(res, val);
        return;

        static long Fn(long v)
        {
            return v + 1;
        }
    }
}