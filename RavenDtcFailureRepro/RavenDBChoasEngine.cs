using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;

public class RavenDBChoasEngine : IWantToRunWhenEndpointStartsAndStops
{
    private Task loopTask;
    private CancellationTokenSource cancellation;

    public Task Start(IMessageSession session)
    {
        cancellation = new CancellationTokenSource();
        loopTask = Loop(cancellation.Token);
        return Task.CompletedTask;
    }

    static async Task Loop(CancellationToken token)
    {
        int iterations = 0;
        var r = new Random();
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(17500 + r.Next(10000), token).ConfigureAwait(false);

                if (++iterations % 2 == 0)
                    ChaosHelper.StopRavenDB();
                else
                    ChaosHelper.KillRavenDB();

                ChaosHelper.StartRavenDB();
            }
            catch (Exception ex)
            {
                LogManager.GetLogger<RavenDBChoasEngine>().Warn("Loop", ex);
            }
        }
    }

    public Task Stop(IMessageSession session)
    {
        cancellation.Cancel();
        return loopTask;
    }
}