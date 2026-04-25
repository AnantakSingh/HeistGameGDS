using UnityEngine;

public class Valuable : MonoBehaviour
{
    [Header("Valuable Settings")]
    [Tooltip("Amount added to the player's score when stolen.")]
    public int value = 100;
    
    [Header("Audio")]
    public AudioClip valuableSound;
    
    [Header("UI Interaction")]
    [Tooltip("Drag the interaction UI Text GameObject here (e.g. 'Press E to Steal').")]
    public GameObject interactUI;

    private PlayerController playerController;
    private bool inRange = false;

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
        if (playerController == null) return;

        // Wait for player to press 'E' or Left Click while in range
        if (inRange && (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)))
        {
            if (valuableSound != null) AudioSource.PlayClipAtPoint(valuableSound, transform.position);
            
            // Add to score
            playerController.AddScore(value);
            
            // Trigger the global stolen state so guards will attack
            playerController.hasStolenSomething = true; 
            
            // Clean up the UI before destroying
            if (interactUI != null) // Keep it clean
            {
                interactUI.SetActive(false);
            }
            
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
        {
            inRange = true;
            if (interactUI != null)
            {
                TMPro.TextMeshProUGUI textComponent = interactUI.GetComponent<TMPro.TextMeshProUGUI>();
                if (textComponent != null)
                {
                    textComponent.text = "Press E to Steal ($" + value + ")";
                }
                interactUI.SetActive(true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
        {
            inRange = false;
            if (interactUI != null)
            {
                interactUI.SetActive(false);
            }
        }
    }
}
