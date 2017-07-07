
# RavenDB DTC is not reliable

Timeout data gets duplicated with any version of RavenDB when distributed transactions (MSDTC) is used. This is caused by an incorrect MSDTC behavior of RaveDB when RavenDB process is crashes/killed and potentially also when connectivity is lost.

## How to reproduce

Please try these steps to reproduce:

1. Start **RavenDB** and the **RavenDtcFailureRepro** endpoint. Dispatch the messages from the unit test.
   
2. Wait until all the messages have been processed and the timouts starts to kick in which is indicated by the series of `.` characters in the console.
   
3. Restart or kill/start RavenDB and wait for the Endpoint to crash as it will not be able recover from this state. Restart the endpoit and see if timeouts documents which should have been deleted will now reappear again. This is indicated by the saga as it detects which messages it already processed.
   
4. Open the RavenDB management studio and navigate to `TimeoutTesterSagaDatas` documents look for *IterationCounts* that have a number higher then 1. These indicate that the message have been received more-than-once which should not have happened if MSDTC is used.

## Workaround

Using native transactions in combination with Outbox resolves these issues and is the only reliable solution to prevent corruption or (message) data loss when using RavenDB.
