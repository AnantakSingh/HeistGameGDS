using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ExitZone : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip gameFinishSound;

    private void Start()
    {
        // Automatically ensure this object's collider is a trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object passing through the exit zone is the Player
        PlayerController player = other.GetComponent<PlayerController>();
        
        // Only trigger the exit if the player is actually carrying stolen goods
        if (player != null && player.hasStolenSomething)
        {
            Debug.Log("Escaped! Triggering End Screen.");
            
            // Silence the alarm if ringing
            if (player.alarmAudioSource != null && player.alarmAudioSource.isPlaying)
            {
                player.alarmAudioSource.Stop();
            }
            
            if (gameFinishSound != null)
            {
                AudioSource.PlayClipAtPoint(gameFinishSound, transform.position);
            }
            
            // Trigger the Victory / Game Finish UI
            if (player.gameFinishUI != null)
            {
                player.gameFinishUI.SetActive(true);
            }
            
            // Show the restart button
            if (player.playAgainButton != null)
            {
                player.playAgainButton.SetActive(true);
            }
            
            // Show the background panel
            if (player.endBackgroundObject != null)
            {
                player.endBackgroundObject.SetActive(true);
            }
            
            // Unlock mouse so they can click the button
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Freeze the game time, preventing any further gameplay
            Time.timeScale = 0f;
        }
    }
}
