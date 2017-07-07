using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using NServiceBus.Logging;

public static class ChaosHelper
{
    private static readonly ILog Log = LogManager.GetLogger("ChaosHelper");

    public static bool IsRunning()
    {
        using (ServiceController service = new ServiceController("RavenDB"))
        {
            return service.Status != ServiceControllerStatus.Stopped;
        }
    }

    public static void StartRavenDB()
    {
        try
        {
            Log.Warn("Starting...");

            using (ServiceController service = new ServiceController("RavenDB"))
            {
                if (service.Status == ServiceControllerStatus.Running) return;
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(3000));
            }
        }
        catch (Exception ex)
        {
            Log.Error("Starting error: " + ex.Message);
        }
        finally
        {
            Log.Warn("Started...");
        }
    }

    public static void StopRavenDB()
    {
        try
        {
            Log.Warn("Stopping...");
            using (ServiceController service = new ServiceController("RavenDB"))
            {
                if (service.Status == ServiceControllerStatus.Stopped) return;
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(3000));
            }
        }
        catch (Exception ex)
        {
            Log.Error("Stopping error: " + ex.Message);
        }
        finally
        {
            Log.Warn("Stopped...");
        }
    }

    public static void KillRavenDB()
    {
        try
        {
            Log.Warn("Killing...");
            Process
                .GetProcessesByName("Raven.Server")
                .FirstOrDefault()
                ?.Kill();
            Log.Warn("Killed...");
        }
        catch (Exception ex)
        {
            Log.Error("Killing error: " + ex.Message);
        }
    }
}