using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NServiceBus;

[TestClass]
public class DeferMeDispatcher
{
    [TestMethod]
    public void SendDeferMe()
    {
        var numberOfMessages = 100;
        var endpointConfiguration = new EndpointConfiguration(endpointName: "SagaTimeoutRepro.MessageDispatcher");
        endpointConfiguration.UsePersistence<InMemoryPersistence>();
        endpointConfiguration.EnableInstallers();
        endpointConfiguration.UseSerialization<JsonSerializer>();
        endpointConfiguration.SendOnly();
        endpointConfiguration.SendFailedMessagesTo("error");

        // Initialize the endpoint with the finished configuration
        var messageSession = Endpoint.Start(endpointConfiguration)
            .ConfigureAwait(false).GetAwaiter().GetResult();

        var tasks = new List<Task>(numberOfMessages);

        for (int i = 1; i <= numberOfMessages; i++)
        {
            var msg = new DeferMe { Ref = i };

            tasks.Add(messageSession.Send("SagaTimeoutRepro", msg));
        };

        Task.WhenAll(tasks).GetAwaiter().GetResult();
        messageSession.Stop();
    }
}
