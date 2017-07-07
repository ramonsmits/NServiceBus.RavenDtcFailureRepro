using NServiceBus;
using NServiceBus.Logging;
using Raven.Client.Document;
using Raven.Client.Document.DTC;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

public class EndpointConfig : IConfigureThisEndpoint
{
    private ILog Log;

    static Mutex SingleInstanceMutex = AcquireGlobalMutex("Receiver");

    static Mutex AcquireGlobalMutex(string id, int durationInSeconds = 5)
    {
        var m = new Mutex(true, @"Global\" + id);
        var maxWaitDuration = TimeSpan.FromSeconds(durationInSeconds);
        if (!m.WaitOne(maxWaitDuration)) throw new InvalidOperationException("Failed to acquire mutex.");
        return m;
    }

    public EndpointConfig()
    {
        log4net.Config.XmlConfigurator.Configure();
        NServiceBus.Logging.LogManager.Use<NServiceBus.Log4NetFactory>();
        Log = NServiceBus.Logging.LogManager.GetLogger("EndpointConfig");

        InitAppDomainEventLogging();
    }

    static void InitAppDomainEventLogging()
    {
        ChaosHelper.StartRavenDB();

        var firstChanceLog = LogManager.GetLogger("FirstChanceException");
        var unhandledLog = LogManager.GetLogger("UnhandledException");
        var domain = AppDomain.CurrentDomain;

        domain.FirstChanceException += (o, ea) => { firstChanceLog.Debug(ea.Exception.Message, ea.Exception); };
        domain.UnhandledException += (o, ea) =>
        {
            var exception = ea.ExceptionObject as Exception;
            if (exception != null) unhandledLog.Fatal(exception.Message, exception);
        };
    }

    public void Customize(EndpointConfiguration endpointConfiguration)
    {
        endpointConfiguration.DefineEndpointName("SagaTimeoutRepro");
        endpointConfiguration.UseSerialization<JsonSerializer>();
        endpointConfiguration.EnableInstallers();

        var ravenDbUrl = "http://localhost:8080";
        var defaultDatabase = "C";
        var resourceManagerId = new Guid("586bff74-ea9a-44a6-a9b1-5e0ed4d09fac");


        var dtcRecoveryBasePath = Path.GetTempPath();
        var recoveryPath = Path.Combine(dtcRecoveryBasePath, "SagaTimeoutRepro-NServiceBus.RavenDB");

        // Make sure this path is on a FAST but DURABLE drive, usually TEMP folder is configured to be on a fast drive.
        Directory.CreateDirectory(recoveryPath);

        Log.Info("Initializing RavenDB DocumentStore...");
        // http://docs.particular.net/nservicebus/ravendb/manual-dtc-settings#configuring-safe-settings
        var documentStore = new DocumentStore
        {
            Url = ravenDbUrl,
            DefaultDatabase = defaultDatabase,
            ResourceManagerId = resourceManagerId,
            TransactionRecoveryStorage = new LocalDirectoryTransactionRecoveryStorage(recoveryPath)
        }.Initialize();
        Log.Info("RavenDB DocumentStore initialized!");

        endpointConfiguration.UsePersistence<RavenDBPersistence>().SetDefaultDocumentStore(documentStore);
        var recoverability = endpointConfiguration.Recoverability();
        recoverability.Immediate(
            immediate =>
            {
                immediate.NumberOfRetries(3);
            });
        recoverability.Delayed(
            delayed =>
            {
                delayed.NumberOfRetries(2).TimeIncrease(TimeSpan.FromSeconds(10));
            });

        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.AuditProcessedMessagesTo("audit");

        var outbox = false;
        if (outbox)
        {
            // When using outbox we can ramp up the concurrency alot. Don't do this when using MSDTC as RavenDB its DTC implementation acts strange if you do
            endpointConfiguration.LimitMessageProcessingConcurrencyTo(32 * Environment.ProcessorCount);
            endpointConfiguration.EnableOutbox();
            endpointConfiguration.SetTimeToKeepDeduplicationData(TimeSpan.FromHours(1));
            endpointConfiguration.SetFrequencyToRunDeduplicationDataCleanup(TimeSpan.FromSeconds(5));
        }
        else
        {
            // If handlers are idempotent you could drop DTC usage and lower to SendsAtomicWithReceive : https://docs.particular.net/nservicebus/transports/transactions
            //var transport = endpointConfiguration.UseTransport<MsmqTransport>();
        }
        endpointConfiguration.TimeToWaitBeforeTriggeringCriticalErrorOnTimeoutOutages(TimeSpan.FromSeconds(15));
        endpointConfiguration.DefineCriticalErrorAction(OnCriticalError);
    }

    async Task OnCriticalError(ICriticalErrorContext context)
    {
        try
        {
            // To leave the process active, dispose the bus.
            // When the bus is disposed, the attempt to send message will cause an ObjectDisposedException.
            await context.Stop().ConfigureAwait(false);
            //Process.Start("NServiceBus.Host.exe");
        }
        finally
        {
            Environment.FailFast($"Critical error shutting down:'{context.Error}'.", context.Exception);
        }
    }
}
