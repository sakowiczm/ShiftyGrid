namespace ShiftyGrid.Common;

internal class InstanceManager : IDisposable
{
    private const string MutexName = "Global\\ShiftyGrid_41133EE3-2B18-4803-AD57-5B299D3BBBC5";
    private Mutex? mutex;
    private bool instanceExists;

    public bool IsSingleInstance()
    {
        try
        {
            mutex = new Mutex(
                initiallyOwned: true,
                name: MutexName,
                createdNew: out bool createdNew);

            if (createdNew)
            {
                instanceExists = true;
                return true;
            }

            instanceExists = mutex.WaitOne(0);
            return instanceExists;
        }
        catch (AbandonedMutexException)
        {
            instanceExists = true;
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"InstanceManager. Error checking instance.", ex);
            Console.WriteLine($"InstanceManager. Error checking instance.", ex);
            return false;
        }
    }

    public void Dispose()
    {
        if (mutex != null && instanceExists)
        {
            try
            {
                mutex.ReleaseMutex();
                instanceExists = false;
            }
            catch (Exception ex)
            {
                Logger.Error($"InstanceManager. Error releasing instance lock.", ex);
                Console.WriteLine($"InstanceManager. Error releasing instance lock.", ex);
            }
        }

        mutex?.Dispose();
        mutex = null;
    }
}