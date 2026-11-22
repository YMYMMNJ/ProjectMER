using Interactables.Interobjects.DoorUtils;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMER.Features.Extensions.ObjectTypeExtensions
{
    public static class DoorTypeExtensions
    {
        public static DoorVariant GetDoorPrefab(this DoorType doortype)
        {

            DoorVariant prefab = doortype switch
            {
                DoorType.Lcz => PrefabManager.DoorLcz,
                DoorType.Hcz => PrefabManager.DoorHcz,
                DoorType.Ez => PrefabManager.DoorEz,
                DoorType.Bulkdoor => PrefabManager.DoorHeavyBulk,
                DoorType.Gate => PrefabManager.DoorGate,
                _ => throw new InvalidOperationException(),
            };

            return prefab;
        }
    }
}
