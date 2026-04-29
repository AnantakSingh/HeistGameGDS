using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to the cube that unlocks the camera-hack mechanic.
/// Give the cube a Trigger Collider. When the Player enters the zone and
/// presses <see cref="pickupKey"/> the cube is collected, the global
/// <see cref="HasTool"/> flag is set to true, and
/// <see cref="CameraHackZone"/> will allow hacking from that point onward.
/// </summary>
public class CameraHackTool : MonoBehaviour
{
    // ── Global flag ───────────────────────────────────────────────────────────
    /// <summary>True once the player has picked up the hack tool.</summary>
    public static bool HasTool { get; private set; } = false;

    [Header("Pickup")]
    [Tooltip("Key the player must press while standing in the zone to collect the tool.")]
    public KeyCode pickupKey = KeyCode.E;

    [Tooltip("Optional: UI element to show while the player is in range (e.g. 'Press E to pick up').")]
    public GameObject pickupPromptUI;

    [Header("Optional Feedback")]
    [Tooltip("Optional UI element to show when the tool is picked up (e.g. '🔧 Hack Tool Acquired!').")]
    public GameObject pickupNotificationUI;

    [Tooltip("How long the notification stays on screen before hiding automatically. 0 = never hide.")]
    public float notificationDuration = 3f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private bool playerInZone = false;

    // ── Unity Messages ────────────────────────────────────────────────────────
    void Start()
    {
        // Reset the flag when a new scene loads so it doesn't carry over
        // between Play sessions in the Editor.
        HasTool = false;

        if (pickupPromptUI != null)
            pickupPromptUI.SetActive(false);

        if (pickupNotificationUI != null)
            pickupNotificationUI.SetActive(false);
    }

    void Update()
    {
        if (playerInZone && Input.GetKeyDown(pickupKey))
        {
            Collect();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInZone = true;

        if (pickupPromptUI != null)
            pickupPromptUI.SetActive(true);

        Debug.Log("[CameraHackTool] Player entered pickup zone.");
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInZone = false;

        if (pickupPromptUI != null)
            pickupPromptUI.SetActive(false);

        Debug.Log("[CameraHackTool] Player left pickup zone.");
    }

    // ── Pickup logic ──────────────────────────────────────────────────────────
    void Collect()
    {
        HasTool = true;
        Debug.Log("[CameraHackTool] Player collected the camera hack tool!");

        // Hide the prompt
        if (pickupPromptUI != null)
            pickupPromptUI.SetActive(false);

        // Immediately make the cube invisible and non-interactive so it feels
        // collected, but keep the GameObject alive so the coroutine can run.
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = false;

        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        // Show optional pickup notification
        if (pickupNotificationUI != null)
        {
            pickupNotificationUI.SetActive(true);

            if (notificationDuration > 0f)
            {
                StartCoroutine(HideNotificationAfterDelay());
                // Defer destruction so the coroutine above is not killed early.
                Destroy(gameObject, notificationDuration);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    IEnumerator HideNotificationAfterDelay()
    {
        yield return new WaitForSeconds(notificationDuration);

        if (pickupNotificationUI != null)
            pickupNotificationUI.SetActive(false);
    }
}
