using UnityEngine;

/// <summary>
/// Attach to the child trigger-collider GameObject of a Door.
/// Forwards OnTriggerEnter / OnTriggerExit events up to the parent <see cref="Door"/>.
///
/// Setup:
///  1. Add a child empty GameObject to your door prefab (e.g. "DoorZone").
///  2. Add a Collider to it and tick "Is Trigger".
///  3. Add this script to the same child GameObject.
///  4. The parent must have a <see cref="Door"/> component.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorTriggerZone : MonoBehaviour
{
    private Door parentDoor;

    void Awake()
    {
        parentDoor = GetComponentInParent<Door>();
        if (parentDoor == null)
            Debug.LogWarning($"[DoorTriggerZone] '{name}' could not find a Door component on its parent!");

        // Safety: make sure our collider is flagged as trigger
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (parentDoor != null)
            parentDoor.EntityEntered(other);
    }

    void OnTriggerExit(Collider other)
    {
        if (parentDoor != null)
            parentDoor.EntityExited(other);
    }
}
