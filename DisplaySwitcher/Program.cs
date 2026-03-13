namespace DisplaySwitcher;

using System.Threading;

/// <summary>
/// Custom entry point with single-instance enforcement via named Mutex.
/// </summary>
public static class Program
{
    private const string MutexName = "Global\\DisplaySwitcher_SingleInstance";

    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance is already running
            return;
        }

        Microsoft.UI.Xaml.Application.Start(p =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
