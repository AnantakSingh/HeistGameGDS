using UnityEngine;

public class Sandwich : MonoBehaviour
{
    [Header("Sandwich Settings")]
    public float speedBoostAmount = 2f;
    public float jumpBoostAmount = 1f;
    public float rotationSpeed = 45f;
    
    [Header("Audio")]
    public AudioClip sandwichSound;
    
    [Header("UI Interaction")]
    [Tooltip("Drag the interaction UI Text GameObject here (e.g. 'Press E to Eat').")]
    public GameObject interactUI;

    private PlayerController playerController;
    private bool inRange = false;

    private void Start()
    {
        playerController = FindObjectOfType<PlayerController>();
        
        if (interactUI != null)
        {
            interactUI.SetActive(false);
        }
    }

    void Update()
    {
        // Give the sandwich a nice subtle spin!
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.World);

        if (playerController == null) return;

        if (inRange && (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)))
        {
            if (sandwichSound != null) AudioSource.PlayClipAtPoint(sandwichSound, transform.position);
            
            // Add to player score
            playerController.AddScore(10);

            // Notify every guard — only those with LOS right now will witness the theft
            foreach (Guard guard in FindObjectsOfType<Guard>())
                guard.AlertIfWitnessingTheft();

            // Trigger the global stolen state so cameras and guards respond
            playerController.hasStolenSomething = true;
            
            // Pick a random stat to boost (0 = Speed, 1 = Jump)
            int randomChoice = Random.Range(0, 2);
            
            if (randomChoice == 0)
            {
                playerController.BoostWalkSpeed(speedBoostAmount);
            }
            else
            {
                playerController.BoostJumpHeight(jumpBoostAmount);
            }
            
            if (interactUI != null)
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
                interactUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
        {
            inRange = false;
            if (interactUI != null)
                interactUI.SetActive(false);
        }
    }
}
