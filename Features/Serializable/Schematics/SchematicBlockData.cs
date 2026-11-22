using AdminToys;
using Interactables.Interobjects.DoorUtils;
using InventorySystem.Items.Firearms.Attachments;
using LabApi.Features.Wrappers;
using MapGeneration;
using MapGeneration.Distributors;
using Mirror;
using ProjectMER.Events.Handlers.Internal;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Extensions.ObjectTypeExtensions;
using ProjectMER.Features.Objects;
using System.Reflection.Emit;
using UnityEngine;
using CameraType = ProjectMER.Features.Enums.CameraType;
using LightSourceToy = AdminToys.LightSourceToy;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;
using TextToy = AdminToys.TextToy;
using WaypointToy = AdminToys.WaypointToy;

namespace ProjectMER.Features.Serializable.Schematics;

public class SchematicBlockData
{
	public virtual string Name { get; set; }

	public virtual int ObjectId { get; set; }

	public virtual int ParentId { get; set; }

	public virtual string AnimatorName { get; set; }

	public virtual Vector3 Position { get; set; }

	public virtual Vector3 Rotation { get; set; }

	public virtual Vector3 Scale { get; set; }

	public virtual BlockType BlockType { get; set; }

	public virtual Dictionary<string, object> Properties { get; set; }

	public GameObject Create(SchematicObject schematicObject, Transform parentTransform)
	{
		Room? room = Room.GetRoomAtPosition(schematicObject.Position) ?? Room.Get(RoomName.Outside).First();
        GameObject gameObject = BlockType switch
		{
			BlockType.Empty => CreateEmpty(),
			BlockType.Primitive => CreatePrimitive(),
			BlockType.Light => CreateLight(),
			BlockType.Pickup => CreatePickup(schematicObject),
			BlockType.Workstation => CreateWorkstation(),
			BlockType.Text => CreateText(),
			BlockType.Interactable => CreateInteractable(),
			BlockType.Waypoint => CreateWaypoint(),
			BlockType.Door => CreateDoor(),
			BlockType.Camera => CreateCamera(schematicObject),
			_ => CreateEmpty(true)
		};

		gameObject.name = Name;

		var pos = Position;
		var rot = Quaternion.Euler(Rotation);

        Transform transform = gameObject.transform;

		if (!gameObject.TryGetComponent(out DoorVariant door))
		{
            transform.SetParent(parentTransform);
            transform.SetLocalPositionAndRotation(pos, rot);
        }
		else
		{
			//なぜ正しい部屋に沸かせるためにこんな遠回りをしなければならないのか。
			door.Rooms = [room?.Base];

			pos = room.Transform.TransformPoint(pos);
			rot = Quaternion.Euler(Rotation + room.Rotation.eulerAngles);

			transform.SetPositionAndRotation(pos, rot);

			Logger.Info(transform.position);
			Logger.Info(Position);
            NetworkServer.UnSpawn(gameObject);
            NetworkServer.Spawn(gameObject);
        }

        if (BlockType == BlockType.Waypoint)
			gameObject.GetComponent<WaypointToy>().NetworkBoundsSize = Scale;
		else
			transform.localScale = BlockType switch
			{
				BlockType.Empty when Scale == Vector3.zero => Vector3.one,
				_ => Scale
			};

		if (gameObject.TryGetComponent(out AdminToyBase adminToyBase))
		{
			if (Properties != null && Properties.TryGetValue("Static", out object isStatic) && Convert.ToBoolean(isStatic))
			{
				adminToyBase.NetworkIsStatic = true;
			}
			else
			{
				adminToyBase.NetworkMovementSmoothing = 60;
			}
		}

        return gameObject;
	}

	private GameObject CreateEmpty(bool fallback = false)
	{
		if (fallback)
			Logger.Warn($"{BlockType} is not yet implemented. Object will be an empty GameObject instead.");

		PrimitiveObjectToy primitive = GameObject.Instantiate(PrefabManager.PrimitiveObject);
		primitive.NetworkPrimitiveFlags = PrimitiveFlags.None;

		return primitive.gameObject;
	}

	private GameObject CreatePrimitive()
	{
		PrimitiveObjectToy primitive = GameObject.Instantiate(PrefabManager.PrimitiveObject);

		primitive.NetworkPrimitiveType = (PrimitiveType)Convert.ToInt32(Properties["PrimitiveType"]);
		primitive.NetworkMaterialColor = Properties["Color"].ToString().GetColorFromString();

		PrimitiveFlags primitiveFlags;
		if (Properties.TryGetValue("PrimitiveFlags", out object flags))
		{
			primitiveFlags = (PrimitiveFlags)Convert.ToByte(flags);
		}
		else
		{
			// Backward compatibility
			primitiveFlags = PrimitiveFlags.Visible;
			if (Scale.x >= 0f)
				primitiveFlags |= PrimitiveFlags.Collidable;
		}

		primitive.NetworkPrimitiveFlags = primitiveFlags;

		return primitive.gameObject;
	}

	private GameObject CreateLight()
	{
		LightSourceToy light = GameObject.Instantiate(PrefabManager.LightSource);

		light.NetworkLightType = Properties.TryGetValue("LightType", out object lightType) ? (LightType)Convert.ToInt32(lightType) : LightType.Point;
		light.NetworkLightColor = Properties["Color"].ToString().GetColorFromString();
		light.NetworkLightIntensity = Convert.ToSingle(Properties["Intensity"]);
		light.NetworkLightRange = Convert.ToSingle(Properties["Range"]);

		if (Properties.TryGetValue("Shadows", out object shadows))
		{
			// Backward compatibility
			light.NetworkShadowType = Convert.ToBoolean(shadows) ? LightShadows.Soft : LightShadows.None;
		}
		else
		{
			light.NetworkShadowType = (LightShadows)Convert.ToInt32(Properties["ShadowType"]);
			light.NetworkLightShape = (LightShape)Convert.ToInt32(Properties["Shape"]);
			light.NetworkSpotAngle = Convert.ToSingle(Properties["SpotAngle"]);
			light.NetworkInnerSpotAngle = Convert.ToSingle(Properties["InnerSpotAngle"]);
			light.NetworkShadowStrength = Convert.ToSingle(Properties["ShadowStrength"]);
		}

		return light.gameObject;
	}

	private GameObject CreatePickup(SchematicObject schematicObject)
	{
		if (Properties.TryGetValue("Chance", out object property) && UnityEngine.Random.Range(0, 101) > Convert.ToSingle(property))
			return new("Empty Pickup");

		Pickup pickup = Pickup.Create((ItemType)Convert.ToInt32(Properties["ItemType"]), Vector3.zero)!;
		if (Properties.ContainsKey("Locked"))
			PickupEventsHandler.ButtonPickups.Add(pickup.Serial, schematicObject);

		return pickup.GameObject;
	}

	private GameObject CreateWorkstation()
	{
		WorkstationController workstation = GameObject.Instantiate(PrefabManager.Workstation);
		workstation.NetworkStatus = (byte)(Properties.TryGetValue("IsInteractable", out object isInteractable) && Convert.ToBoolean(isInteractable) ? 0 : 4);

		return workstation.gameObject;
	}

	private GameObject CreateText()
	{
		TextToy text = GameObject.Instantiate(PrefabManager.Text);

		text.TextFormat = Convert.ToString(Properties["Text"]);
		text.DisplaySize = Properties["DisplaySize"].ToVector2() * 20f;

		return text.gameObject;
	}

	private GameObject CreateInteractable()
	{
		InvisibleInteractableToy interactable = GameObject.Instantiate(PrefabManager.Interactable);
		interactable.NetworkShape = (InvisibleInteractableToy.ColliderShape)Convert.ToInt32(Properties["Shape"]);
		interactable.NetworkInteractionDuration = Convert.ToSingle(Properties["InteractionDuration"]);
		interactable.NetworkIsLocked = Properties.TryGetValue("IsLocked", out object isLocked) && Convert.ToBoolean(isLocked);

		return interactable.gameObject;
	}

	private GameObject CreateWaypoint()
	{
		WaypointToy waypoint = GameObject.Instantiate(PrefabManager.Waypoint);
		waypoint.NetworkPriority = byte.MaxValue;

		return waypoint.gameObject;
	}
	private GameObject CreateDoor()
	{
		DoorType doortype = (DoorType)Convert.ToInt32(Properties["DoorType"]); 

		var door = GameObject.Instantiate(doortype.GetDoorPrefab());

        if (door.TryGetComponent(out DoorRandomInitialStateExtension doorRandomInitialStateExtension))
            GameObject.Destroy(doorRandomInitialStateExtension);

		SetupDoor();

        return door.gameObject;

		void SetupDoor()
		{
            bool reqAll = Convert.ToBoolean(Properties["RequireAll"]);
            var perm = (DoorPermissionFlags)Convert.ToInt16(Properties["RequiredPermissions"]);
            bool IsOpen = Convert.ToBoolean(Properties["IsOpen"]);
            bool IsLocked = Convert.ToBoolean(Properties["IsLocked"]);
            
			door.NetworkTargetState = IsOpen;
			door.ServerChangeLock(DoorLockReason.SpecialDoorFeature, IsLocked);
            door.RequiredPermissions = new DoorPermissionsPolicy(perm, reqAll);
        }
    }
	private GameObject CreateCamera(SchematicObject schematicObject)
	{
		var room = Room.GetRoomAtPosition(schematicObject.Position);

		CameraType camtype = (CameraType)Convert.ToInt32(Properties["CameraType"]); 
		var cam = GameObject.Instantiate(camtype.GetCameraPrefab());
        cam.NetworkMovementSmoothing = 60;
        cam.NetworkRoom = room == null ? Room.Get(RoomName.Outside).First().Base : room.Base;

        if (Properties.TryGetValue("Label", out object val))
		{
			string label = Convert.ToString(val);
            cam.NetworkLabel = label;
        }

        return cam.gameObject;
    }
}
