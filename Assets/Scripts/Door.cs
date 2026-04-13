using UnityEngine;

public class Door : MonoBehaviour
{
    [Header("Door Settings")]
    [Tooltip("If true, the player needs at least 1 key to open this door.")]
    public bool requiresKey = false;
    
    [Tooltip("Distance at which the door opens when the player comes close.")]
    public float openRange = 2f;
    
    [Tooltip("How far down the door goes when opened.")]
    public float openDepth = 3f;
    
    [Tooltip("Speed of the door opening/closing.")]
    public float lerpSpeed = 5f;

    [Header("Audio")]
    public AudioClip openSound;
    public AudioClip lockedSound;
    
    // Simple cooldown variable so it doesn't spam the locked sound every frame the player stands there
    private float soundCooldown = 0f;

    private Vector3 closedPosition;
    private Vector3 openPosition;
    private bool isOpen = false;
    private Transform playerTransform;
    private PlayerController playerController;

    void Start()
    {
        closedPosition = transform.position;
        openPosition = closedPosition + Vector3.down * openDepth;
        
        playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerTransform = playerController.transform;
        }
        else
        {
            Debug.LogWarning("PlayerController not found in the scene! Door won't be able to detect the player.");
        }
    }

    void Update()
    {
        bool anyEntityNear = false;

        // Reduce cooldown timer
        if (soundCooldown > 0f) soundCooldown -= Time.deltaTime;

        // 1. Check player distance
        if (playerTransform != null && Vector3.Distance(closedPosition, playerTransform.position) <= openRange)
        {
            // If it's locked, try to unlock
            if (requiresKey)
            {
                if (playerController != null && playerController.keyCount > 0)
                {
                    playerController.keyCount--;
                    requiresKey = false;
                }
                else
                {
                    // The player does not have a key and it is locked
                    if (soundCooldown <= 0f && lockedSound != null)
                    {
                        AudioSource.PlayClipAtPoint(lockedSound, transform.position);
                        soundCooldown = 2f; // Wait 2 seconds before playing the locked sound again
                    }
                }
            }
            
            // If it's unlocked, player can open it
            if (!requiresKey)
            {
                anyEntityNear = true;
            }
        }

        // 2. Check for guards using OverlapSphere if player hasn't already triggered it
        if (!anyEntityNear)
        {
            Collider[] colliders = Physics.OverlapSphere(closedPosition, openRange);
            foreach (Collider col in colliders)
            {
                if (col.CompareTag("Guard"))
                {
                    Guard guardScript = col.GetComponent<Guard>();
                    
                    // Allow the guard to open if the door is already unlocked, OR if the guard is chasing the player
                    if (!requiresKey || (guardScript != null && guardScript.IsChasing))
                    {
                        anyEntityNear = true;
                        requiresKey = false; // Guards bypass locks and permanently unlock the door
                        break;
                    }
                }
            }
        }
        
        // Only trigger open sound when it transitions from closed to open
        if (!isOpen && anyEntityNear)
        {
            if (openSound != null) AudioSource.PlayClipAtPoint(openSound, transform.position);
        }

        isOpen = anyEntityNear;

        // Apply Lerp movement
        Vector3 targetPosition = isOpen ? openPosition : closedPosition;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);
    }
}
