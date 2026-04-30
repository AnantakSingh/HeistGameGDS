using UnityEngine;

public class Key : MonoBehaviour
{
    [Header("Key Settings")]
    public float rotationSpeed = 90f;
    
    [Header("Audio")]
    public AudioClip keySound;
    
    [Header("UI Interaction")]
    [Tooltip("Drag a UI Canvas Prefab here to spawn it when the player is close.")]
    public GameObject interactUIPrefab;
    
    [Tooltip("How high above the key should the UI spawn?")]
    public float uiSpawnHeight = 1f;

    private PlayerController playerController;
    private bool inRange = false;
    private GameObject spawnedUI; // Keeps track of the prefab we spawn

    private void Start()
    {
        playerController = FindObjectOfType<PlayerController>();
    }

    void Update()
    {
        // Visual float effect and spin
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.World);

        // Make the UI hover above the key manually so it doesn't inherit the spinning rotation or weird scales
        if (spawnedUI != null)
        {
            spawnedUI.transform.position = transform.position + Vector3.up * uiSpawnHeight;
        }

        if (playerController == null) return;

        // Wait for player to press 'E' or Left Click while in range
        if (inRange && (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)))
        {
            if (keySound != null) AudioSource.PlayClipAtPoint(keySound, transform.position);
            
            // Add key to inventory (keys are used for doors, not score)
            playerController.keyCount++;
            
            // Trigger the global stolen state so guards will attack
            playerController.hasStolenSomething = true; 
            
            // Clean up the UI before the key destroys itself
            if (spawnedUI != null)
            {
                Destroy(spawnedUI);
            }
            
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
        {
            inRange = true;
            
            // Spawn the UI Prefab above the key if we haven't already
            if (interactUIPrefab != null && spawnedUI == null)
            {
                Vector3 spawnPos = transform.position + Vector3.up * uiSpawnHeight;
                // Instantiate WITHOUT making it a child, so it ignores the Key's rotation
                spawnedUI = Instantiate(interactUIPrefab, spawnPos, Quaternion.identity);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
        {
            inRange = false;
            
            // Destroy the UI when walking away
            if (spawnedUI != null)
            {
                Destroy(spawnedUI);
            }
        }
    }
}
