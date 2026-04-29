using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;
#if MONO
using ScheduleOne.Property;

#else
using Il2CppScheduleOne.Property;
#endif


namespace BusinessEmployment.Helpers;

public static class AlternateIdlePointRegistry
{
    private static Dictionary<string, IdlePoint> _idlePoints = new()
    {
        {
            "laundromat", new IdlePoint
            {
                Position = new Vector3(-23.93f, 0.1f, 21f),
                Rotation = Quaternion.Euler(0f, 90f, 0f)
            }
        },
        {
            "tacoticklers", new IdlePoint
            {
                Position = new Vector3(-33.96f, 0.1f, 84.32f),
                Rotation = Quaternion.Euler(0f, 0f, 0f)
            }
        },
        {
            "carwash", new IdlePoint
            {
                Position = new Vector3(-8.05f, 0.1f, -15.76f),
                Rotation = Quaternion.Euler(0f, 235f, 0f)
            }
        },
        {
            "postoffice", new IdlePoint
            {
                Position = new Vector3(43.54f, 0.1f, -3.88f),
                Rotation = Quaternion.Euler(0f, 180f, 0f)
            }
        },
    };

    public static Transform GetPointTransform(Business business)
    {
        if (!_idlePoints.TryGetValue(business.propertyCode, out var idlePointData))
        {
            MelonLogger.Warning($"Alternate position for {business.propertyCode} not found, using default.");
            var idlePoint = Object.Instantiate(business.SpawnPoint, business.SpawnPoint.position,
                business.SpawnPoint.rotation);
            return idlePoint;
        }
        else
        {
            var idlePoint = Object.Instantiate(business.SpawnPoint, idlePointData.Position, idlePointData.Rotation);
            return idlePoint.transform;
        }
    }
}

internal record IdlePoint
{
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
}