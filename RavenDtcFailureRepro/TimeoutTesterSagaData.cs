using System.Collections.Generic;
using NServiceBus;

public class TimeoutTesterSagaData : ContainSagaData
{
    public List<string> MessageIds { get; set; } = new List<string>();
    public int Ref { get; set; }
    public List<int> IterationCounts = new List<int>();
}