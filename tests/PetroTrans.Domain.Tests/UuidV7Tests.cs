using PetroTrans.Domain;
using PetroTrans.Domain.Identity;
using Xunit;

namespace PetroTrans.Domain.Tests;

public class UuidV7Tests
{
    [Fact]
    public void New_UsesVersion7()
    {
        var id = UuidV7.New();
        var bytes = id.ToByteArray(bigEndian: true);
        Assert.Equal(0x70, bytes[6] & 0xF0);
        Assert.Equal(0x80, bytes[8] & 0xC0);
    }

    [Fact]
    public void New_CreatesDistinctValues()
    {
        var first = UuidV7.New();
        var second = UuidV7.New();
        Assert.NotEqual(first, second);
    }
}

public class PermissionCatalogTests
{
    [Fact]
    public void PricingOverride_CodeIsLocked()
    {
        Assert.Equal("pricing.override", PermissionCodes.PricingOverride);
        Assert.Equal("owner_manager", RoleCodes.OwnerManager);
        Assert.Equal("operator", RoleCodes.Operator);
    }
}
