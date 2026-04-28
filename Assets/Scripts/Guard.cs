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

    [Tooltip("How long the player must stay inside the vision collider before the guard enters chase mode. " +
             "Gives the player a window to duck out of sight.")]
    public float chaseGraceTime = 0.5f;
    private float visionGraceTimer = 0f;  // counts UP while player is continuously in vision

    [Header("Detection Details")]
    [Tooltip("The angle of the guard's vision cone.")]
    public float fovAngle = 110f;
    
    [Tooltip("Layers that block the guard's vision (walls, etc).")]
    public LayerMask obstructionMask;

    [Tooltip("The height from the guard's feet to their eyes for raycasting.")]
    public float eyeHeight = 1.5f;

    [Header("Audio")]
    public AudioSource movementAudioSource;
    public AudioClip walkSound;
    public AudioClip runSound;

    [Header("Animation")]
    [Tooltip("Drag the child GameObject with the Animator here")]
    public Animator guardAnimator;

    [Header("UI Indicators")]
    [Tooltip("Drag the Sphere MeshRenderer here to indicate guard state.")]
    public MeshRenderer stateIndicator;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private PlayerController playerController;

    private enum State { Roam, Investigate, Chase }
    private State currentState;
    private float chaseTimer = 0f;

    [Header("Camera Alert Settings")]
    [Tooltip("How long the guard pauses at the investigate point before returning to roam.")]
    public float investigateLingerTime = 4f;
    private float investigateTimer = 0f;
    private Vector3 investigateTarget;

    [Header("Patrol Behaviour")]
    [Tooltip("Range of seconds the guard wanders before stopping (min, max).")]
    public Vector2 wanderDuration = new Vector2(5f, 10f);

    [Tooltip("Range of seconds the guard stands still before wandering again (min, max).")]
    public Vector2 stillDuration = new Vector2(5f, 15f);

    private enum PatrolSubState { Wandering, Standing }
    private PatrolSubState patrolSubState = PatrolSubState.Wandering;
    private float patrolTimer = 0f;   // counts down to next sub-state switch
    
    public bool IsChasing { get { return currentState == State.Chase; } }
    
    /// <summary>
    /// Returns true if the guard is currently investigating or chasing (Yellow or Red states).
    /// </summary>
    public bool IsAlerted { get { return currentState != State.Roam || visionGraceTimer > 0f; } }

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
        // Start in a wander phase with a random duration
        patrolSubState = PatrolSubState.Wandering;
        patrolTimer = Random.Range(wanderDuration.x, wanderDuration.y);
        SetRandomDestination();
    }

    void Update()
    {
        if (playerTransform == null || playerController == null) return;


        switch (currentState)
        {
            case State.Roam:
                // ── Vision / suspicion check (runs regardless of sub-state) ──
                CharacterController playerCC = playerController.GetComponent<CharacterController>();
                float playerSpeed = 0f;
                if (playerCC != null)
                {
                    Vector3 flatVelocity = new Vector3(playerCC.velocity.x, 0, playerCC.velocity.z);
                    playerSpeed = flatVelocity.magnitude;
                }

                bool isRunning = playerSpeed > 5f;
                bool isDoingSomethingSuspicious = playerController.hasStolenSomething || isRunning;
                bool permanentLookout = SecurityCamera.CameraAlertTriggered;

                // New Logic: 
                // 1. Can we SEE the player? (FOV + Raycast)
                // 2. Can we HEAR the player? (Proximity + Running)
                bool canSee = CanSeePlayer();
                bool canHear = isPlayerInVision && isRunning;

                if ((canSee && (isDoingSomethingSuspicious || permanentLookout)) || canHear)
                {
                    // Count up while player stays in vision/hearing — only chase after the grace period
                    visionGraceTimer += Time.deltaTime;
                    if (visionGraceTimer >= chaseGraceTime)
                    {
                        visionGraceTimer = 0f;
                        agent.isStopped  = false;
                        currentState     = State.Chase;
                        agent.speed      = chaseSpeed;
                        chaseTimer       = chaseLingerTime;
                        break;
                    }
                }
                else
                {
                    // Player left vision/hearing or is no longer suspicious — reset the grace window
                    visionGraceTimer = 0f;
                }

                // ── Patrol sub-state machine ──
                patrolTimer -= Time.deltaTime;

                switch (patrolSubState)
                {
                    case PatrolSubState.Wandering:
                        // Keep walking; if we reached the current waypoint, pick a new one
                        if (!agent.pathPending && agent.remainingDistance < 0.5f)
                            SetRandomDestination();

                        // Time to stop and stand still?
                        if (patrolTimer <= 0f)
                        {
                            patrolSubState = PatrolSubState.Standing;
                            patrolTimer    = Random.Range(stillDuration.x, stillDuration.y);
                            agent.isStopped = true;   // halts NavMesh movement cleanly
                        }
                        break;

                    case PatrolSubState.Standing:
                        // Time to start wandering again?
                        if (patrolTimer <= 0f)
                        {
                            patrolSubState  = PatrolSubState.Wandering;
                            patrolTimer     = Random.Range(wanderDuration.x, wanderDuration.y);
                            agent.isStopped = false;
                            SetRandomDestination();
                        }
                        break;
                }
                break;

            case State.Investigate:
                agent.isStopped = false;
                agent.SetDestination(investigateTarget);

                // Once we arrive, wait briefly then return to roam
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    investigateTimer -= Time.deltaTime;
                    if (investigateTimer <= 0f)
                    {
                        currentState   = State.Roam;
                        agent.speed    = roamSpeed;
                        patrolSubState = PatrolSubState.Wandering;
                        patrolTimer    = Random.Range(wanderDuration.x, wanderDuration.y);
                        SetRandomDestination();
                    }
                }

                // If the player is seen or heard while investigating, give them the grace period to hide
                bool isRunningInv = playerController.GetComponent<CharacterController>().velocity.magnitude > 5f;
                bool canSeeInv = CanSeePlayer();
                bool canHearInv = isPlayerInVision && isRunningInv;

                if (canSeeInv || canHearInv)
                {
                    visionGraceTimer += Time.deltaTime;
                    if (visionGraceTimer >= chaseGraceTime)
                    {
                        visionGraceTimer = 0f;
                        currentState     = State.Chase;
                        agent.speed      = chaseSpeed;
                        chaseTimer       = chaseLingerTime;
                    }
                }
                else
                {
                    visionGraceTimer = 0f;
                }
                break;

            case State.Chase:
                // Follow the player
                agent.SetDestination(playerTransform.position);
                
                // Track chase persistence
                // Guard stays in chase as long as they can see/hear the player, or during the linger period
                bool isRunningChase = playerController.GetComponent<CharacterController>().velocity.magnitude > 5f;
                if (CanSeePlayer() || (isPlayerInVision && isRunningChase))
                {
                    // Keep the timer fully replenished
                    chaseTimer = chaseLingerTime;
                }
                else
                {
                    // Player stepped out of the vision box trigger, count down the lose-aggro timer
                    chaseTimer -= Time.deltaTime;
                    
                    if (chaseTimer <= 0f)
                        {
                            // Lost the player; return to roam and start a fresh wander phase
                            currentState   = State.Roam;
                            agent.speed    = roamSpeed;
                            agent.isStopped = false;
                            patrolSubState = PatrolSubState.Wandering;
                            patrolTimer    = Random.Range(wanderDuration.x, wanderDuration.y);
                            SetRandomDestination();
                        }              }

                // Check physical distance purely for catching/game over
                if (Vector3.Distance(transform.position, playerTransform.position) <= catchDistance)
                {
                    playerController.TriggerGameOver();
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

        // --- Animation Logic ---
        if (guardAnimator != null)
        {
            float targetSpeed = 0f;
            
            // If the guard is actively moving
            if (agent.velocity.sqrMagnitude > 0.01f)
            {
                // Walk (0.5) when roaming, Run (1.0) when investigating or chasing
                targetSpeed = (currentState == State.Roam) ? 0.5f : 1f;
            }
            
            // Smoothly blend the speed parameter
            guardAnimator.SetFloat("Speed", targetSpeed, 0.1f, Time.deltaTime);
        }

        // --- State Indicator Logic ---
        if (stateIndicator != null)
        {
            if (currentState == State.Chase)
            {
                stateIndicator.material.color = Color.red;
            }
            else if (currentState == State.Investigate || visionGraceTimer > 0f)
            {
                // Yellow if investigating a camera alert, or if the player is in vision and we are getting suspicious
                stateIndicator.material.color = Color.yellow;
            }
            else
            {
                stateIndicator.material.color = Color.green;
            }
        }
    }

    /// <summary>
    /// Called by SecurityCamera when a theft is detected on-camera.
    /// Sends this guard to investigate the camera's world position.
    /// </summary>
    public void InvestigatePoint(Vector3 worldPosition)
    {
        investigateTarget = worldPosition;
        investigateTimer = investigateLingerTime;
        currentState = State.Investigate;
        agent.speed = chaseSpeed; // Run to the scene of the crime
        agent.SetDestination(worldPosition);
        Debug.Log($"[Guard] '{name}' is investigating camera alert at {worldPosition}");
    }

    private bool CanSeePlayer()
    {
        if (!isPlayerInVision || playerTransform == null) return false;

        // 1. Angle Check (FOV)
        // Check from guard eye level to player center level
        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        Vector3 targetCenter = playerTransform.position + Vector3.up * 1.0f; 
        Vector3 toPlayer = (targetCenter - eyePos).normalized;
        
        float angle = Vector3.Angle(transform.forward, toPlayer);

        if (angle > fovAngle * 0.5f) return false;

        // 2. Line of Sight Check (Raycast)
        float dist = Vector3.Distance(eyePos, targetCenter);

        if (Physics.Raycast(eyePos, toPlayer, out RaycastHit hit, dist, obstructionMask))
        {
            // If the ray hits something that isn't the player, vision is blocked
            if (hit.collider.transform.root != playerTransform.root && hit.collider.GetComponentInParent<PlayerController>() == null)
            {
                return false;
            }
        }

        return true;
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
