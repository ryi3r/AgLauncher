using System.Threading;

public class ValueQueue
{
    public Mutex Mutex = new();
    public long CurrentValue;

    public ValueQueue() { }

    public void Add(long value)
    {
        Mutex.WaitOne();
        try
        {
            CurrentValue += value;
        }
        finally
        {
            Mutex.ReleaseMutex();
        }
    }

    public void Remove(long value)
    {
        Mutex.WaitOne();
        try
        {
            CurrentValue -= value;
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
        }
        finally
        {
            Mutex.ReleaseMutex();
        }
    }
}