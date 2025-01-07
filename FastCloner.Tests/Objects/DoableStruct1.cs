namespace FastCloner.Tests.Objects;

public struct DoableStruct1 : IDoable
{
    public int X;

    public int Do() => ++X;
}