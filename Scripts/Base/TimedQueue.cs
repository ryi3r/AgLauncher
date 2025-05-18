using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

public class TimedQueue
{
    public Mutex Mutex = new();
    public Dictionary<long, long> Data = [];
    public long CurrentValue;
    public Stopwatch Timer = new();

    public TimedQueue() { }

    public void Update()
    {
        Mutex.WaitOne();
        try
        {
            var currentTick = Timer.ElapsedTicks;
            foreach (var key in Data.Keys.ToArray())
            {
                if (currentTick > key)
                {
                    CurrentValue -= Data[key];
                    Data.Remove(key);
                }
            }
        }
        finally
        {
            Mutex.ReleaseMutex();
        }
    }

    public void Add(long value)
    {
        Mutex.WaitOne();
        try
        {
            Data[Timer.ElapsedTicks + Stopwatch.Frequency] = value;
            CurrentValue += value;
        }
        finally
        {
            Mutex.ReleaseMutex();
        }
    }

    // this is the safe option to obtain the current value
    public long GetValue()
    {
        Mutex.WaitOne();
        try
        {
            return CurrentValue;
        }
        finally
        {
            Mutex.ReleaseMutex();
        }
    }

    public void Reset()
    {
        Mutex.WaitOne();
        try
        {
            CurrentValue = 0;
            Data.Clear();
            Timer.Stop();
        }
        finally
        {
            Mutex.ReleaseMutex();
        }
    }

    public void Start()
    {
        Mutex.WaitOne();
        try
        {
            Timer.Start();
        }
        finally
        {
            Mutex.ReleaseMutex();
        }
    }
}