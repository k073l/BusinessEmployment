namespace BusinessEmployment.Persistence;

public record LaunderBehaviourSaveData
{
    public string PropertyCode { get; set; }
    public float MoneyLeftToLaunder { get; set; }
}