using System.Collections;
using TMPro;
using UnityEngine;

public class MinimapExitTrigger : MonoBehaviour
{
    [Tooltip("The exit object that has the MinimapMarker script on it.")]
    public MinimapMarker exitMarker;

    [Header("Notification UI")]
    [Tooltip("TextMeshProUGUI element on the HUD that displays the 'marked on map' message.")]
    public TextMeshProUGUI notificationText;

    [Tooltip("How long (seconds) the notification stays on screen.")]
    public float notificationDuration = 2f;

    private bool triggered = false;

    private void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // Ensure the notification is hidden at the start
        if (notificationText != null) notificationText.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
        {
            triggered = true;

            // Show the exit on the minimap
            if (exitMarker != null) exitMarker.SetVisible(true);

            // Show the on-screen notification
            if (notificationText != null)
                StartCoroutine(ShowNotification());

            Debug.Log("Exit marked on minimap!");
        }
    }

    private IEnumerator ShowNotification()
    {
        notificationText.text = "This exit has been marked on your map";
        notificationText.gameObject.SetActive(true);
        yield return new WaitForSeconds(notificationDuration);
        notificationText.gameObject.SetActive(false);
    }
}
