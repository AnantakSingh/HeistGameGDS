using UnityEngine;

public class Laser : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Check if the object that entered the laser is the Player
        PlayerController player = other.GetComponent<PlayerController>();
        
        if (player != null)
        {
            Debug.Log("Player hit a laser!");
            player.TriggerGameOver();
        }
    }
}
