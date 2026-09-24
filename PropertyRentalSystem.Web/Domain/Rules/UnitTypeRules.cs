namespace PropertyRentalSystem.Web.Domain.Rules;

public static class UnitTypeRules
{
    // "An inactive value still displays on a unit that already uses it but cannot be
    // selected for any other unit." currentUnitTypeId is the unit's existing type when
    // editing (null when creating a new unit).
    public static bool CanAssign(bool candidateIsActive, int candidateUnitTypeId, int? currentUnitTypeId)
    {
        if (currentUnitTypeId.HasValue && candidateUnitTypeId == currentUnitTypeId.Value)
            return true; // left unchanged, allowed even if inactive

        return candidateIsActive;
    }
}
