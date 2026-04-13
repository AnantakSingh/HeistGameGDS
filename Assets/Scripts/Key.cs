using UnityEngine;

public class Key : MonoBehaviour
{
    [Header("Key Settings")]
    public float rotationSpeed = 90f;
    public float pickupRadius = 2f;
    
    [Header("Audio")]
    public AudioClip keySound;
    
    [Header("UI Interaction")]
    [Tooltip("Drag the interaction UI Text GameObject here. It will hide/show based on distance.")]
    public GameObject interactUI;

    private PlayerController playerController;

    private bool wasInRange = false;

    private void Start()
    {
        playerController = FindObjectOfType<PlayerController>();
        
        // Ensure UI is hidden at start
        if (interactUI != null)
        {
            interactUI.SetActive(false);
        }
    }

    void Update()
    {
        // Visual float effect and spin
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.World);

        if (playerController == null) return;

        // Check 2m radius
        float distance = Vector3.Distance(transform.position, playerController.transform.position);
        bool inRange = distance <= pickupRadius;
        
        // Toggle the UI based ONLY on enter/exit transitions to prevent multiple objects fighting over the same UI
        if (interactUI != null)
        {
            if (inRange && !wasInRange)
            {
                interactUI.SetActive(true);
                wasInRange = true;
            }
            else if (!inRange && wasInRange)
            {
                interactUI.SetActive(false);
                wasInRange = false;
            }
        }

        // Wait for player to press 'E' or Left Click while in range
        if (inRange && (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)))
        {
            if (keySound != null) AudioSource.PlayClipAtPoint(keySound, transform.position);
            
            // Add key to inventory and score
            playerController.keyCount++;
            playerController.AddScore(10);
            
            // Trigger the global stolen state so guards will attack
            playerController.hasStolenSomething = true; 
            
            // Clean up the UI before the key destroys itself
            if (interactUI != null)
            {
                interactUI.SetActive(false);
            }
            
            Destroy(gameObject);
        }
    }
}
