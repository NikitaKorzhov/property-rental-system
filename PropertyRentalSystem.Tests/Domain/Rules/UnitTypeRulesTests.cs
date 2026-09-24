using PropertyRentalSystem.Web.Domain.Rules;

namespace PropertyRentalSystem.Tests.Domain.Rules;

public class UnitTypeRulesTests
{
    [Fact]
    public void CanAssign_ActiveType_ForNewUnit_ReturnsTrue()
    {
        var result = UnitTypeRules.CanAssign(candidateIsActive: true, candidateUnitTypeId: 1, currentUnitTypeId: null);

        Assert.True(result);
    }

    [Fact]
    public void CanAssign_InactiveType_ForNewUnit_ReturnsFalse()
    {
        var result = UnitTypeRules.CanAssign(candidateIsActive: false, candidateUnitTypeId: 3, currentUnitTypeId: null);

        Assert.False(result);
    }

    [Fact]
    public void CanAssign_InactiveType_LeftUnchangedOnEdit_ReturnsTrue()
    {
        // The unit already has this (now inactive) type and the edit doesn't change it —
        // "an inactive value still displays on a unit that already uses it".
        var result = UnitTypeRules.CanAssign(candidateIsActive: false, candidateUnitTypeId: 3, currentUnitTypeId: 3);

        Assert.True(result);
    }

    [Fact]
    public void CanAssign_InactiveType_SwitchedToFromAnotherType_ReturnsFalse()
    {
        // "cannot be selected for any other unit" — also covers switching *this* unit onto
        // an inactive type it didn't already have.
        var result = UnitTypeRules.CanAssign(candidateIsActive: false, candidateUnitTypeId: 3, currentUnitTypeId: 1);

        Assert.False(result);
    }

    [Fact]
    public void CanAssign_ActiveType_SwitchedOnEdit_ReturnsTrue()
    {
        var result = UnitTypeRules.CanAssign(candidateIsActive: true, candidateUnitTypeId: 2, currentUnitTypeId: 1);

        Assert.True(result);
    }
}
