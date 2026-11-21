using AdminToys;
using InventorySystem.Items.Firearms.Attachments;
using LabApi.Features.Wrappers;
using ProjectMER.Events.Handlers.Internal;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Objects;
using UnityEngine;
using CameraType = ProjectMER.Features.Enums.CameraType;
using LightSourceToy = AdminToys.LightSourceToy;
using Locker = MapGeneration.Distributors.Locker;
using LockerType = ProjectMER.Features.Enums.LockerType;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;
using Scp079CameraToy = AdminToys.Scp079CameraToy;
using StructurePositionSync = MapGeneration.Distributors.StructurePositionSync;
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
			BlockType.Locker => CreateLocker(),
			BlockType.Camera => CreateCamera(),
			_ => CreateEmpty(true)
		};

		gameObject.name = Name;

		Transform transform = gameObject.transform;
		transform.SetParent(parentTransform);
		transform.SetLocalPositionAndRotation(Position, Quaternion.Euler(Rotation));
		
		if (BlockType == BlockType.Waypoint)
			gameObject.GetComponent<WaypointToy>().NetworkBoundsSize = Scale;
		else
			transform.localScale = BlockType switch
			{
				BlockType.Empty when Scale == Vector3.zero => Vector3.one,
				_ => Scale
			};

		// These values depend on the final world transform, so they can only be set after the block has been parented and positioned.
		if (BlockType == BlockType.Locker && gameObject.TryGetComponent(out StructurePositionSync structurePositionSync))
		{
			structurePositionSync.Network_position = transform.position;
			structurePositionSync.Network_rotationY = (sbyte)Mathf.RoundToInt(transform.rotation.eulerAngles.y / 5.625f);
		}
		else if (BlockType == BlockType.Camera && gameObject.TryGetComponent(out Scp079CameraToy cameraToy))
		{
			cameraToy.NetworkScale = transform.localScale;
			cameraToy.NetworkRoom = RoomExtensions.GetRoomAtPosition(transform.position).Base;
		}

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

	private GameObject CreateLocker()
	{
		LockerType lockerType = GetEnumProperty("LockerType", LockerType.PedestalScp500);

		Locker? prefab = PrefabManager.GetLocker(lockerType);
		if (prefab == null)
		{
			Logger.Warn($"Locker block \"{Name}\" has an invalid LockerType ({lockerType}). Object will be an empty GameObject instead.");
			return CreateEmpty();
		}

		return GameObject.Instantiate(prefab).gameObject;
	}

	private GameObject CreateCamera()
	{
		CameraType cameraType = GetEnumProperty("CameraType", CameraType.Lcz);

		Scp079CameraToy? prefab = PrefabManager.GetCamera(cameraType);
		if (prefab == null)
		{
			Logger.Warn($"Camera block \"{Name}\" has an invalid CameraType ({cameraType}). Object will be an empty GameObject instead.");
			return CreateEmpty();
		}

		Scp079CameraToy camera = GameObject.Instantiate(prefab);
		camera.NetworkLabel = Properties != null && Properties.TryGetValue("Label", out object label) && label != null ? Convert.ToString(label) : "CustomCamera";

		return camera.gameObject;
	}

	/// <summary>
	/// Reads an enum from <see cref="Properties"/>. Accepts either the enum name (case-insensitive) or its numeric value.
	/// </summary>
	private T GetEnumProperty<T>(string key, T fallback) where T : struct, Enum
	{
		if (Properties == null || !Properties.TryGetValue(key, out object value) || value == null)
			return fallback;

		if (value is string text)
			return Enum.TryParse(text, true, out T parsed) ? parsed : fallback;

		try
		{
			return (T)Enum.ToObject(typeof(T), Convert.ToInt32(value));
		}
		catch (Exception)
		{
			return fallback;
		}
	}
}
