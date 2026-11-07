using Microsoft.UI.Dispatching;

namespace UniversalTvRemote.WinUI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instance = new Mutex(true, @"Local\app.braviaremote.controller", out var firstInstance);
        if (!firstInstance)
            return;

        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Microsoft.UI.Xaml.Application.Start(args =>
            {
                var dispatcher = DispatcherQueue.GetForCurrentThread();
                SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(dispatcher));
                _ = new App();
            });
        }
        finally
        {
            instance.ReleaseMutex();
        }
    }
}
