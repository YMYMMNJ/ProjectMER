using AdminToys;
using PlayerRoles.PlayableScps.Scp079.Cameras;
using ProjectMER.Features.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMER.Features.Extensions.ObjectTypeExtensions
{
    public static class CameraTypeExtensions
    {
        public static Scp079CameraToy GetCameraPrefab(this CameraType camtype)
        {
            Scp079CameraToy prefab = camtype switch
            {
                CameraType.Lcz => PrefabManager.CameraLcz,
                CameraType.Hcz => PrefabManager.CameraHcz,
                CameraType.Ez => PrefabManager.CameraEz,
                CameraType.EzArm => PrefabManager.CameraEzArm,
                CameraType.Sz => PrefabManager.CameraSz,
                _ => throw new InvalidOperationException(),
            };

            return prefab;
        }
    }
}
