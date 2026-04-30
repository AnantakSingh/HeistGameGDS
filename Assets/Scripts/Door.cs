using UnityEngine;
using TMPro;

/// <summary>
/// Attach this script to the root Door GameObject.
///
/// Setup:
///  1. Add a child empty GameObject (e.g. "DoorZone") with a Trigger Collider
///     sized to cover the area where you want the door to react.
///  2. Add the <see cref="DoorTriggerZone"/> script to that child.
///  3. Assign a world-space or screen-space UI prompt object to <see cref="promptUI"/>.
///     Optionally assign its TextMeshPro child to <see cref="promptText"/> to get
///     dynamic messages ("Press E to open" / "This door requires a key").
///  4. Player tag = "Player", Guard tag = "Guard".
/// </summary>
public class Door : MonoBehaviour
{
    [Header("Door Settings")]
    [Tooltip("If true, the player needs at least 1 key to open this door.")]
    public bool requiresKey = false;

    [Tooltip("How far down the door slides when opened.")]
    public float openDepth = 3f;

    [Tooltip("Speed of the door opening / closing lerp.")]
    public float lerpSpeed = 8f;

    [Tooltip("Seconds after the trigger zone empties before the door starts closing.")]
    public float closeDelay = 1f;

    [Tooltip("Key the player must press to open the door.")]
    public KeyCode interactKey = KeyCode.E;

    [Header("UI Prompt")]
    [Tooltip("Drag the screen-space TextMeshProUGUI element here. It will be shown/hidden automatically.")]
    public TextMeshProUGUI promptText;

    [Header("Audio")]
    public AudioClip openSound;
    public AudioClip lockedSound;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private Vector3 closedPosition;
    private Vector3 openPosition;

    private bool  isOpen    = false;
    private bool  isMoving  = false;
    private float closeTimer = 0f;

    private int playerCount = 0;
    private int guardCount  = 0;

    private float soundCooldown = 0f;
    private PlayerController playerController;

    // ── Unity Messages ────────────────────────────────────────────────────────
    void Start()
    {
        closedPosition   = transform.position;
        openPosition     = closedPosition + Vector3.down * openDepth;
        playerController = FindObjectOfType<PlayerController>();

        if (playerController == null)
            Debug.LogWarning("[Door] PlayerController not found in scene.");

        if (promptText != null) promptText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (soundCooldown > 0f) soundCooldown -= Time.deltaTime;

        bool playerInZone = playerCount > 0;
        bool guardInZone  = guardCount  > 0;
        bool anyInZone    = playerInZone || guardInZone;

        // ── Player interaction ────────────────────────────────────────────
        if (playerInZone && !isOpen)
        {
            UpdatePrompt();

            if (Input.GetKeyDown(interactKey))
            {
                if (requiresKey)
                {
                    if (playerController != null && playerController.keyCount > 0)
                    {
                        // Consume one key and unlock
                        playerController.keyCount--;
                        requiresKey = false;
                        OpenDoor();
                    }
                    else
                    {
                        // Locked — audio feedback
                        if (soundCooldown <= 0f && lockedSound != null)
                        {
                            AudioSource.PlayClipAtPoint(lockedSound, transform.position);
                            soundCooldown = 2f;
                        }
                    }
                }
                else
                {
                    OpenDoor();
                }
            }
        }

        // ── Guards auto-open (no key press needed) ────────────────────────
        if (guardInZone && !isOpen)
        {
            requiresKey = false; // Guards bypass and permanently unlock
            OpenDoor();
        }

        // ── Hide prompt when door is open or player leaves ────────────────
        if (promptText != null && (!playerInZone || isOpen))
            promptText.gameObject.SetActive(false);

        // ── Close delay ───────────────────────────────────────────────────
        if (!anyInZone && isOpen)
        {
            closeTimer -= Time.deltaTime;
            if (closeTimer <= 0f)
                CloseDoor();
        }
        else if (anyInZone)
        {
            closeTimer = closeDelay;
        }

        // ── Lerp to target ────────────────────────────────────────────────
        if (isMoving)
        {
            Vector3 target     = isOpen ? openPosition : closedPosition;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * lerpSpeed);

            if (Vector3.Distance(transform.position, target) < 0.005f)
            {
                transform.position = target;
                isMoving = false;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    void OpenDoor()
    {
        if (isOpen) return;
        isOpen     = true;
        isMoving   = true;
        closeTimer = closeDelay;

        if (promptText != null) promptText.gameObject.SetActive(false);
        if (openSound != null) AudioSource.PlayClipAtPoint(openSound, transform.position);
    }

    void CloseDoor()
    {
        if (!isOpen) return;
        isOpen   = false;
        isMoving = true;
    }

    void UpdatePrompt()
    {
        if (promptText == null) return;

        bool playerHasKey = playerController != null && playerController.keyCount > 0;
        promptText.text = (requiresKey && !playerHasKey)
            ? "This door requires a key"
            : "Press E to open";

        promptText.gameObject.SetActive(true);
    }

    // ── Called by DoorTriggerZone ─────────────────────────────────────────────
    public void EntityEntered(Collider other)
    {
        if (other.CompareTag("Player"))     playerCount++;
        else if (other.CompareTag("Guard")) guardCount++;
    }

    public void EntityExited(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerCount = Mathf.Max(0, playerCount - 1);
            if (playerCount == 0 && promptText != null)
                promptText.gameObject.SetActive(false);
        }
        else if (other.CompareTag("Guard"))
        {
            guardCount = Mathf.Max(0, guardCount - 1);
        }
    }
}
