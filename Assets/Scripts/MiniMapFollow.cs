using UnityEngine;

public class MiniMapFollow : MonoBehaviour
{
    public Transform target;  // player
    public bool followTarget = false; // Set to true if you want the camera to follow the player
    public float height = 20f; // height of the camera

    void LateUpdate()
    {
        if (followTarget && target != null)
        {
            transform.position = new Vector3(
                target.position.x,
                target.position.y + height,
                target.position.z
            );
        }

        // Keep the camera looking straight down
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}