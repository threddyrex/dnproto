namespace dnproto.tests.repo;

using dnproto.sdk.repo;

public class VarIntTests
{
    [Fact]
    public void WriteAndReadVarInt_RoundTrip()
    {
        // Test various values including edge cases
        var testValues = new[] { 0, 1, 127, 128, 255, 256, 16383, 16384, 2097151, 2097152, int.MaxValue};

        foreach (var testValue in testValues)
        {
            // Arrange
            var originalVarInt = new VarInt { Value = testValue };
            using var stream = new MemoryStream();

            // Act - Write
            VarInt.WriteVarInt(stream, originalVarInt);

            // Act - Read
            stream.Position = 0;
            var readVarInt = VarInt.ReadVarInt(stream);

            // Assert
            Assert.Equal(originalVarInt.Value, readVarInt.Value);
        }
    }

    [Fact]
    public void WriteVarInt_SingleByte()
    {
        // Arrange
        var varInt = new VarInt { Value = 1 };
        using var stream = new MemoryStream();

        // Act
        VarInt.WriteVarInt(stream, varInt);

        // Assert
        stream.Position = 0;
        Assert.Equal(1, stream.Length);
        Assert.Equal(1, stream.ReadByte());
    }

    [Fact]
    public void WriteVarInt_MultiByte()
    {
        // Arrange
        var varInt = new VarInt { Value = 300 };
        using var stream = new MemoryStream();

        // Act
        VarInt.WriteVarInt(stream, varInt);

        // Assert
        stream.Position = 0;
        Assert.Equal(2, stream.Length);
        // 300 = 0b100101100
        // First byte: 0b10101100 (0xAC) - lower 7 bits (0101100) with continuation bit set
        // Second byte: 0b00000010 (0x02) - remaining bits (10)
        Assert.Equal(0xAC, stream.ReadByte());
        Assert.Equal(0x02, stream.ReadByte());
    }

    [Fact]
    public void ReadVarInt_SingleByte()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0x01 });

        // Act
        var varInt = VarInt.ReadVarInt(stream);

        // Assert
        Assert.Equal(1, varInt.Value);
    }

    [Fact]
    public void ReadVarInt_MultiByte()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0xAC, 0x02 });

        // Act
        var varInt = VarInt.ReadVarInt(stream);

        // Assert
        Assert.Equal(300, varInt.Value);
    }
}
