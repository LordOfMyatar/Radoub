using Radoub.Formats.Gff;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Round-trip tests for GFF 64-bit field types (DWORD64, INT64, DOUBLE).
/// Issue #2241 — these types are misclassified as simple types and corrupt on round-trip.
/// </summary>
public class Gff64BitRoundTripTests
{
    private const ulong Dword64Value = 0x123456789ABCDEF0UL;
    private const long Int64Value = -1234567890123456789L;
    private const double DoubleValue = 3.141592653589793;

    private static GffFile BuildGffWithField(uint type, string label, object value)
    {
        var gff = new GffFile { FileType = "TST ", FileVersion = "V3.2" };
        gff.RootStruct.Type = 0xFFFFFFFF;
        gff.RootStruct.Fields.Add(new GffField { Label = label, Type = type, Value = value });
        return gff;
    }

    [Fact]
    public void RoundTrip_Dword64_PreservesFullValue()
    {
        var gff = BuildGffWithField(GffField.DWORD64, "Dword64Field", Dword64Value);

        var bytes = GffWriter.Write(gff);
        var parsed = GffReader.Read(bytes);

        var field = parsed.RootStruct.GetField("Dword64Field");
        Assert.NotNull(field);
        Assert.Equal(GffField.DWORD64, field.Type);
        Assert.Equal(Dword64Value, (ulong)field.Value!);
    }

    [Fact]
    public void RoundTrip_Int64_PreservesFullValue()
    {
        var gff = BuildGffWithField(GffField.INT64, "Int64Field", Int64Value);

        var bytes = GffWriter.Write(gff);
        var parsed = GffReader.Read(bytes);

        var field = parsed.RootStruct.GetField("Int64Field");
        Assert.NotNull(field);
        Assert.Equal(GffField.INT64, field.Type);
        Assert.Equal(Int64Value, (long)field.Value!);
    }

    [Fact]
    public void RoundTrip_Double_PreservesFullValue()
    {
        var gff = BuildGffWithField(GffField.DOUBLE, "DoubleField", DoubleValue);

        var bytes = GffWriter.Write(gff);
        var parsed = GffReader.Read(bytes);

        var field = parsed.RootStruct.GetField("DoubleField");
        Assert.NotNull(field);
        Assert.Equal(GffField.DOUBLE, field.Type);
        Assert.Equal(DoubleValue, (double)field.Value!);
    }
}
