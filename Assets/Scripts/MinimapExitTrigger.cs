using UnityEngine;

public class MinimapExitTrigger : MonoBehaviour
{
    [Tooltip("The exit object that has the MinimapMarker script on it.")]
    public MinimapMarker exitMarker;

    private void Start()
    {
        // Ensure the collider is a trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the player entered the trigger
        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
        {
            if (exitMarker != null)
            {
                exitMarker.SetVisible(true);
                Debug.Log("Exit marked on minimap!");
            }
            
            // Optionally disable this trigger after use
            // gameObject.SetActive(false);
        }
    }
}
