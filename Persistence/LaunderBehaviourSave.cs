using S1API.Internal.Abstraction;
using S1API.Saveables;

namespace BusinessEmployment.Persistence;

public class LaunderBehaviourSave : Saveable
{
    [SaveableField("BusinessEmploymentLaunderBehaviourSave")]
    public List<LaunderBehaviourSaveData> SaveDatas = [];

    public static LaunderBehaviourSave Instance { get; private set; } = new();

    public LaunderBehaviourSave()
    {
        Instance = this;
    }

    protected override void OnLoaded()
    {
        foreach (var saveData in SaveDatas)
        {
            
        }
    }
}