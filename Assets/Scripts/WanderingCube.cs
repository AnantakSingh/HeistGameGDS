using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class WanderingCube : MonoBehaviour
{
    [Header("Movement Settings")]
    public float roamRadius = 10f;
    public float walkSpeed = 2f;

    [Header("Timings")]
    [Tooltip("Range of seconds to stay idle.")]
    public Vector2 idleDurationRange = new Vector2(10f, 20f);

    [Tooltip("Range of seconds to wander.")]
    public Vector2 wanderDurationRange = new Vector2(5f, 10f);

    [Header("Animation")]
    [Tooltip("Drag the child GameObject with the Animator here.")]
    public Animator cubeAnimator;

    private NavMeshAgent agent;
    private enum State { Idle, Wandering }
    private State currentState;
    private float timer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = walkSpeed;

        // Start in wandering state
        EnterWandering();
    }

    void Update()
    {
        timer -= Time.deltaTime;

        switch (currentState)
        {
            case State.Wandering:
                // If we reached our destination or time is up, switch to idle
                if ((!agent.pathPending && agent.remainingDistance < 0.5f) || timer <= 0f)
                {
                    EnterIdle();
                }
                break;

            case State.Idle:
                // If time is up, start wandering again
                if (timer <= 0f)
                {
                    EnterWandering();
                }
                break;
        }

        // --- Animation Logic ---
        if (cubeAnimator != null)
        {
            float targetSpeed = 0f;

            // If the cube is actively moving on the NavMesh
            if (agent.velocity.sqrMagnitude > 0.01f)
            {
                targetSpeed = 1f; // Full walk speed
            }

            // Smoothly blend the "Speed" parameter in the Animator Controller
            cubeAnimator.SetFloat("Speed", targetSpeed, 0.1f, Time.deltaTime);
        }
    }

    void EnterIdle()
    {
        currentState = State.Idle;
        timer = Random.Range(idleDurationRange.x, idleDurationRange.y);
        agent.isStopped = true; // Stop the agent
        Debug.Log($"[WanderingCube] {name} is now IDLE for {timer:F1} seconds.");
    }

    void EnterWandering()
    {
        currentState = State.Wandering;
        timer = Random.Range(wanderDurationRange.x, wanderDurationRange.y);
        agent.isStopped = false;
        SetRandomDestination();
        Debug.Log($"[WanderingCube] {name} is now WANDERING for {timer:F1} seconds.");
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
}
