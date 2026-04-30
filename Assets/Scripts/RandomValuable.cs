using UnityEngine;

/// <summary>
/// Identical to <see cref="Valuable"/> except the value is randomized
/// on Start to a number between 100 and 5000.
/// </summary>
public class RandomValuable : MonoBehaviour
{
    [Header("Random Value Range")]
    [Tooltip("Minimum random value (inclusive).")]
    public int minValue = 100;

    [Tooltip("Maximum random value (inclusive).")]
    public int maxValue = 5000;

    [Header("Audio")]
    public AudioClip valuableSound;

    [Header("UI Interaction")]
    [Tooltip("Drag the interaction UI Text GameObject here (e.g. 'Press E to Steal').")]
    public GameObject interactUI;

    private int value;
    private PlayerController playerController;
    private bool inRange = false;

    private void Start()
    {
        // Randomize the value on initialization
        value = Random.Range(minValue, maxValue + 1);

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

            // Notify every guard — only those with LOS right now will witness the theft
            foreach (Guard guard in FindObjectsOfType<Guard>())
                guard.AlertIfWitnessingTheft();

            // Trigger the global stolen state so guards will attack
            playerController.hasStolenSomething = true;

            // Clean up the UI before destroying
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
