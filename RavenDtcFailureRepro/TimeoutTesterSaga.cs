using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;

class TimeoutTesterSaga
    : Saga<TimeoutTesterSagaData>
    , IAmStartedByMessages<DeferMe>
{
    static readonly ILog Log = LogManager.GetLogger<TimeoutTesterSaga>();
    static readonly TimeSpan roundingOffset = TimeSpan.FromSeconds(15);

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<TimeoutTesterSagaData> mapper)
    {
        mapper.ConfigureMapping<DeferMe>(m => m.Ref).ToSaga(s => s.Ref);
    }

    public Task Handle(DeferMe message, IMessageHandlerContext context)
    {
        Data.MessageIds.Add(context.MessageId);

        var d = Data.IterationCounts;

        var i = message.Iteration;
        if (i == d.Count)
        {
            d.Add(1);
        }
        else if (i < d.Count)
        {
            ++d[i];
            Log.FatalFormat("Ref: {0}, Iteration: {1} processed {2} times.", message.Ref, message.Iteration, d[i]);
        }
        else
        {
            throw new InvalidOperationException();
        }

        Console.Out.WriteAsync(".");

        // Schedule next iteration
        var at = DateTime.UtcNow.RoundUp(roundingOffset);
        message.Iteration++;
        var options = new SendOptions();
        options.DoNotDeliverBefore(at);
        options.RouteToThisEndpoint();
        return context.Send(message, options);
    }
}