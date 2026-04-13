using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Guard : MonoBehaviour
{
    [Header("Guard Settings")]
    public float roamRadius = 15f;
    public float catchDistance = 1.5f; // We keep catch distance since it dictates physically touching the player
    
    [Header("Speeds")]
    public float roamSpeed = 3f;
    public float chaseSpeed = 6f;
    
    [Header("Chase Settings")]
    [Tooltip("How many seconds the guard will keep chasing after the player leaves the vision box")]
    public float chaseLingerTime = 5f;

    [Header("Audio")]
    public AudioSource movementAudioSource;
    public AudioClip walkSound;
    public AudioClip runSound;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private PlayerController playerController;

    private enum State { Roam, Chase }
    private State currentState;
    private float chaseTimer = 0f;
    
    public bool IsChasing { get { return currentState == State.Chase; } }
    
    // Tracks if player is currently inside the vision trigger box
    private bool isPlayerInVision = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerTransform = playerController.transform;
        }
        
        // Ensure starting state is initialized correctly
        agent.speed = roamSpeed;
        currentState = State.Roam;
        SetRandomDestination();
    }

    void Update()
    {
        if (playerTransform == null || playerController == null) return;

        // GLOBAL ALARM: If the player's timer hit 0, all guards go into permanent chase mode regardless of vision/distance
        if (playerController.hasStolenSomething && playerController.timeRemaining <= 0f)
        {
            currentState = State.Chase;
            agent.speed = chaseSpeed;
            agent.SetDestination(playerTransform.position);
            
            // Check physical distance purely for catching
            if (Vector3.Distance(transform.position, playerTransform.position) <= catchDistance)
            {
                Debug.Log("Player Caught by Guard during global alarm!");
            }
            return; // Skip normal vision/roam logic entirely
        }

        switch (currentState)
        {
            case State.Roam:
                // Calculate player's actual horizontal speed
                CharacterController playerCC = playerController.GetComponent<CharacterController>();
                float playerSpeed = 0f;
                if (playerCC != null)
                {
                    Vector3 flatVelocity = new Vector3(playerCC.velocity.x, 0, playerCC.velocity.z);
                    playerSpeed = flatVelocity.magnitude;
                }

                // Check if player has stolen something OR is moving faster than 4 (walking/running)
                bool isDoingSomethingSuspicious = playerController.hasStolenSomething || playerSpeed > 5f;

                // Fire state change if suspicious AND inside the vision box
                if (isDoingSomethingSuspicious && isPlayerInVision)
                {
                    currentState = State.Chase;
                    agent.speed = chaseSpeed;
                    chaseTimer = chaseLingerTime; // Initialize the linger timer to 5s
                }
                else
                {
                    // If arrived at random destination, pick a new one
                    if (!agent.pathPending && agent.remainingDistance < 0.5f)
                    {
                        SetRandomDestination();
                    }
                }
                break;

            case State.Chase:
                // Follow the player
                agent.SetDestination(playerTransform.position);
                
                // Track chase persistence using vision box
                if (isPlayerInVision)
                {
                    // If player is still inside vision box, keep the timer fully replenished at 5s
                    chaseTimer = chaseLingerTime;
                }
                else
                {
                    // Player stepped out of the vision box trigger, count down the lose-aggro timer
                    chaseTimer -= Time.deltaTime;
                    
                    if (chaseTimer <= 0f)
                    {
                        // 5 seconds have passed without seeing the player, return to roam state
                        currentState = State.Roam;
                        agent.speed = roamSpeed;
                        SetRandomDestination();
                    }
                }

                // Check physical distance purely for catching/game over
                if (Vector3.Distance(transform.position, playerTransform.position) <= catchDistance)
                {
                    Debug.Log("Player Caught by Guard!");
                }
                break;
        }

        // --- Audio Logic ---
        if (agent.velocity.sqrMagnitude > 0.01f) // Guard is actively moving
        {
            AudioClip desiredClip = (currentState == State.Roam) ? walkSound : runSound;
            
            if (movementAudioSource != null && desiredClip != null)
            {
                if (movementAudioSource.clip != desiredClip)
                {
                    movementAudioSource.clip = desiredClip;
                    movementAudioSource.loop = true;
                    movementAudioSource.Play();
                }
                else if (!movementAudioSource.isPlaying)
                {
                    movementAudioSource.Play();
                }
            }
        }
        else
        {
            // Guard is standing still
            if (movementAudioSource != null && movementAudioSource.isPlaying)
            {
                movementAudioSource.Pause();
            }
        }
    }

    void SetRandomDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * roamRadius;
        randomDirection += transform.position;
        
        NavMeshHit navHit;
        // Find closest valid point on the NavMesh
        if (NavMesh.SamplePosition(randomDirection, out navHit, roamRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(navHit.position);
        }
    }

    // Rely on the Vision Box Trigger Colliders
    private void OnTriggerStay(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            isPlayerInVision = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            isPlayerInVision = false;
        }
    }
}
