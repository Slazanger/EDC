namespace EveDataCollator.Data;

public struct DecVector3
{
    public decimal X { get; }
    public decimal Y { get; }
    public decimal Z { get; }

    public DecVector3(decimal x, decimal y, decimal z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}