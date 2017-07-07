using NServiceBus;

public class DeferMe : ICommand
{
    public int Ref { get; set; }
    public int Iteration { get; set; }
}