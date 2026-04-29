using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach this script to a child GameObject of a Security Camera that has a
/// Trigger Collider on it. When the player enters the trigger zone the script
/// disables all MonoBehaviour components on the PARENT camera GameObject for
/// <see cref="disableDuration"/> seconds, then re-enables them.
///
/// Setup:
///  1. Add a child empty GameObject to your camera prefab (e.g. "HackZone").
///  2. Add a Collider to it, tick "Is Trigger".
///  3. Add this script to the same child GameObject.
///  4. Make sure your Player GameObject has the "Player" tag.
/// </summary>
public class CameraHackZone : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("How long (in seconds) the camera stays disabled after the player interacts with it.")]
    public float disableDuration = 5f;

    [Tooltip("Key the player must press while inside the zone to hack the camera. Default: E")]
    public KeyCode hackKey = KeyCode.E;

    [Header("Optional UI Prompt")]
    [Tooltip("Optional: A world-space or screen-space UI object to show while the player is in range.")]
    public GameObject promptUI;

    // ── Runtime ──────────────────────────────────────────────────────────────
    private bool playerInZone   = false;
    private bool isHacking      = false;   // true while the camera is currently disabled

    // Cache the parent's behaviours once at Start so we don't allocate every frame.
    private MonoBehaviour[] parentBehaviours;

    // ── Unity Messages ────────────────────────────────────────────────────────
    void Start()
    {
        // Grab every MonoBehaviour on the PARENT camera object (not this child).
        if (transform.parent != null)
        {
            parentBehaviours = transform.parent.GetComponents<MonoBehaviour>();
        }
        else
        {
            Debug.LogWarning($"[CameraHackZone] '{name}' has no parent! " +
                             "This script must be on a child of the camera GameObject.");
        }

        // Make sure the prompt is hidden at start.
        if (promptUI != null)
            promptUI.SetActive(false);
    }

    void Update()
    {
        // Only listen for the hack key when the player is standing in the zone,
        // has the hack tool, and the camera isn't already disabled.
        if (playerInZone && !isHacking && CameraHackTool.HasTool && Input.GetKeyDown(hackKey))
        {
            StartCoroutine(HackCamera());
        }
    }

    // ── Trigger Callbacks ─────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInZone = true;

        // Only show the prompt if the player already has the hack tool.
        if (promptUI != null && !isHacking && CameraHackTool.HasTool)
            promptUI.SetActive(true);

        Debug.Log($"[CameraHackZone] Player entered hack zone on '{transform.parent?.name}'.");
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInZone = false;

        if (promptUI != null)
            promptUI.SetActive(false);

        Debug.Log($"[CameraHackZone] Player left hack zone on '{transform.parent?.name}'.");
    }

    // ── Coroutine ─────────────────────────────────────────────────────────────
    IEnumerator HackCamera()
    {
        if (parentBehaviours == null || parentBehaviours.Length == 0)
        {
            Debug.LogWarning($"[CameraHackZone] No parent behaviours found on '{transform.parent?.name}'.");
            yield break;
        }

        isHacking = true;

        // Hide the prompt while the camera is hacked.
        if (promptUI != null)
            promptUI.SetActive(false);

        // ── Disable every script on the camera parent ──────────────────────
        List<MonoBehaviour> disabled = new List<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in parentBehaviours)
        {
            // Skip null refs (destroyed components) and skip ourselves.
            if (behaviour == null || behaviour == this) continue;

            if (behaviour.enabled)
            {
                behaviour.enabled = false;
                disabled.Add(behaviour);
            }
        }

        Debug.Log($"[CameraHackZone] Camera '{transform.parent?.name}' HACKED for {disableDuration}s. " +
                  $"Disabled {disabled.Count} script(s).");

        // ── Wait ────────────────────────────────────────────────────────────
        yield return new WaitForSeconds(disableDuration);

        // ── Re-enable all scripts that we disabled ─────────────────────────
        foreach (MonoBehaviour behaviour in disabled)
        {
            if (behaviour != null)
                behaviour.enabled = true;
        }

        isHacking = false;

        // If the player is still standing in the zone, show the prompt again.
        if (promptUI != null && playerInZone)
            promptUI.SetActive(true);

        Debug.Log($"[CameraHackZone] Camera '{transform.parent?.name}' restored after hack.");
    }

    // ── Editor Gizmo ─────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = isHacking ? new Color(0f, 1f, 0.4f, 0.35f) : new Color(0.2f, 0.8f, 1f, 0.25f);

        if (col is BoxCollider box)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(box.center),
                                          transform.rotation,
                                          transform.lossyScale);
            Gizmos.DrawCube(Vector3.zero, box.size);
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, box.size);
            Gizmos.matrix = oldMatrix;
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(transform.TransformPoint(sphere.center),
                              sphere.radius * Mathf.Max(transform.lossyScale.x,
                                                        transform.lossyScale.y,
                                                        transform.lossyScale.z));
        }
    }
#endif
}
