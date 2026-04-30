using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Attach to any security camera GameObject.
/// - Drag the PlayerController and specific Guards into the Inspector.
/// - Casts a configurable FOV cone every frame.
/// - If the player steals while in view, the assigned guards are sent to investigate.
/// </summary>
public class SecurityCamera : MonoBehaviour
{
    // ── Static alert flag ─────────────────────────────────────────────────────
    /// <summary>
    /// Set to true the first time any camera catches a theft.
    /// All guards read this to enter permanent-lookout mode.
    /// </summary>
    public static bool CameraAlertTriggered { get; private set; } = false;

    // ── Inspector fields ───────────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("Drag the Player's PlayerController component here.")]
    public PlayerController playerController;

    [Tooltip("Drag the specific Guards that should investigate when this camera fires.")]
    public Guard[] assignedGuards;

    [Tooltip("The rotating part of the camera. If null, uses the GameObject this script is on.")]
    public Transform cameraHead;

    [Header("Detection Settings")]
    [Tooltip("Full field of view in degrees. Camera faces its local +Z (blue arrow).")]
    public float fieldOfViewAngle = 100f;

    [Tooltip("Maximum detection range in Unity units.")]
    public float viewDistance = 15f;

    [Tooltip("Maximum vertical height difference (±) the camera can detect across.")]
    public float viewHeight = 3f;

    [Tooltip("Layers that block line of sight (walls, etc). Do NOT include the Player layer.")]
    public LayerMask obstructionMask;

    [Header("Night Mode")]
    [Tooltip("When enabled, guards are dispatched instantly whenever the player enters the camera's view — no stealing required.")]
    public bool isNightTime = false;

    [Header("Visualization")]
    [Tooltip("Colour of the FOV cone outline when idle.")]
    public Color idleColor    = new Color(1f, 0.92f, 0f, 0.85f);

    [Tooltip("Colour of the FOV cone outline when the player is detected.")]
    public Color detectedColor = new Color(1f, 0.15f, 0f, 1f);

    [Tooltip("Thickness of the cone lines in world units.")]
    public float lineWidth = 0.04f;

    [Header("Debug")]
    [Tooltip("Print which FOV check is failing to the Console each frame.")]
    public bool showDebugLogs = false;

    // ── Runtime state ──────────────────────────────────────────────────────────
    private Transform playerTransform;
    private bool isPlayerInView = false;
    public bool IsPlayerInView { get { return isPlayerInView; } }

    // Track the score at the time of the last alert so we detect each NEW steal separately.
    // Starts at -1 so the first steal (score goes from 0 → positive) always triggers.
    private int  lastAlertedScore = -1;
    private bool alertPending     = false; // true while the dispatch delay is running
    private bool nightAlertSent   = false; // prevents night-mode from spamming every frame

    // Visualization
    private LineRenderer            rimRenderer;
    private List<LineRenderer>      spokeRenderers = new List<LineRenderer>();

    // ── Unity Messages ─────────────────────────────────────────────────────────
    void Start()
    {
        if (playerController == null)
        {
            Debug.LogError($"[SecurityCamera] '{name}': PlayerController is not assigned in the Inspector!");
            return;
        }
        playerTransform = playerController.transform;
        Debug.Log($"[SecurityCamera] '{name}' initialized. Monitoring player '{playerTransform.name}'.");
        BuildVisualization();
    }

    void Update()
    {
        if (playerTransform == null || playerController == null) return;

        isPlayerInView = CheckPlayerInView();

        // Cone is red while the player is detected OR while a dispatch is pending
        SetVisualizationColor((isPlayerInView || alertPending) ? detectedColor : idleColor);

        // ── Night-time mode: instant alert on sight, no stealing needed ──
        if (isNightTime && isPlayerInView && !nightAlertSent && !alertPending)
        {
            nightAlertSent = true;
            TriggerAlert();
        }

        // Reset the night flag once the player leaves view so re-entry triggers again
        if (isNightTime && !isPlayerInView)
        {
            nightAlertSent = false;
        }

        // ── Normal mode: trigger on every new steal that happens in view ──
        if (!isNightTime && isPlayerInView && playerController.score > lastAlertedScore && playerController.hasStolenSomething)
        {
            TriggerAlert();
        }

        // Only sync the score baseline while the player IS in view.
        // If we synced every frame, a steal that occurred one frame before/after
        // the camera's Update would permanently collapse the score delta to zero,
        // making the alert undetectable. By only advancing the baseline when the
        // camera can actually see the player, we preserve the unseen delta until
        // the player next appears in view.
        if (isPlayerInView)
        {
            lastAlertedScore = playerController.score;
        }
    }

    // ── Detection ──────────────────────────────────────────────────────────────
    bool CheckPlayerInView()
    {
        // Use cameraHead if assigned, otherwise use this transform
        Transform viewSource = (cameraHead != null) ? cameraHead : transform;
        Vector3 toPlayer = playerTransform.position - viewSource.position;

        // 1. Height check
        if (Mathf.Abs(toPlayer.y) > viewHeight)
        {
            if (showDebugLogs) Debug.Log($"[SecurityCamera] '{name}' FAIL: height diff {Mathf.Abs(toPlayer.y):F1} > viewHeight {viewHeight}");
            return false;
        }

        // 2. Distance check
        if (toPlayer.magnitude > viewDistance)
        {
            if (showDebugLogs) Debug.Log($"[SecurityCamera] '{name}' FAIL: distance {toPlayer.magnitude:F1} > viewDistance {viewDistance}");
            return false;
        }

        // 3. Angle check (using the actual forward vector of the viewSource)
        float angle = Vector3.Angle(viewSource.forward, toPlayer);
        if (angle > fieldOfViewAngle * 0.5f)
        {
            if (showDebugLogs) Debug.Log($"[SecurityCamera] '{name}' FAIL: angle {angle:F1}° > halfFOV {fieldOfViewAngle * 0.5f}°");
            return false;
        }

        // 4. Line-of-sight raycast
        Vector3 eyePos    = viewSource.position;
        Vector3 targetPos = playerTransform.position + Vector3.up * 0.8f;
        Vector3 direction = (targetPos - eyePos).normalized;
        float   dist      = Vector3.Distance(eyePos, targetPos);

        if (Physics.Raycast(eyePos, direction, out RaycastHit hit, dist, obstructionMask))
        {
            // If the ray hits the player (or part of the player), we can see them! 
            if (hit.collider.transform.root == playerTransform.root || hit.collider.GetComponentInParent<PlayerController>() != null)
            {
                if (showDebugLogs) Debug.Log($"[SecurityCamera] '{name}' Player IN VIEW (Hit player directly).");
                return true;
            }

            if (showDebugLogs) Debug.Log($"[SecurityCamera] '{name}' FAIL: line-of-sight blocked by {hit.collider.name}");
            return false;
        }

        if (showDebugLogs) Debug.Log($"[SecurityCamera] '{name}' Player IN VIEW.");
        return true;
    }

    // ── Alert ──────────────────────────────────────────────────────────────────
    void TriggerAlert()
    {
        // Latch the global flag so all guards enter permanent-lookout mode
        CameraAlertTriggered = true;
        alertPending         = true;

        // Snapshot the player's position at the moment of the steal
        Vector3 stealPosition = playerTransform.position;

        Debug.Log($"[SecurityCamera] '{name}' caught the player stealing! Guards dispatching in 5 seconds...");
        StartCoroutine(DispatchGuardsDelayed(stealPosition, 2f));
    }

    System.Collections.IEnumerator DispatchGuardsDelayed(Vector3 stealPosition, float delay)
    {
        yield return new WaitForSeconds(delay);

        alertPending = false;
        // Snap to nearest NavMesh point (player is always on walkable ground, so 3 units is plenty)
        Vector3 alertDestination = stealPosition;
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(stealPosition, out navHit, 3f, NavMesh.AllAreas))
        {
            alertDestination = navHit.position;
        }
        else
        {
            Debug.LogWarning($"[SecurityCamera] '{name}' could not snap steal position to NavMesh. Using raw position.");
        }

        if (assignedGuards == null || assignedGuards.Length == 0)
        {
            Debug.LogWarning($"[SecurityCamera] '{name}' has no guards assigned in the Inspector!");
            yield break;
        }

        foreach (Guard guard in assignedGuards)
        {
            if (guard != null)
            {
                guard.InvestigatePoint(alertDestination);
                Debug.Log($"[SecurityCamera] Sent guard '{guard.name}' to steal location {alertDestination}.");
            }
        }
    }

    // ── Runtime visualization ──────────────────────────────────────────────────
    void BuildVisualization()
    {
        float    halfFOV    = fieldOfViewAngle * 0.5f;
        int      rimSegs    = 48;
        int      spokeCount = 8;

        // Compute rim points in local space
        // Base edge: rotate local forward (+Z) by halfFOV around local right (+X)
        Vector3   baseEdge  = Quaternion.AngleAxis(halfFOV, Vector3.right) * Vector3.forward;
        Vector3[] rimPoints = new Vector3[rimSegs];

        for (int i = 0; i < rimSegs; i++)
        {
            float   az      = 360f / rimSegs * i;
            Vector3 edgeDir = Quaternion.AngleAxis(az, Vector3.forward) * baseEdge;
            rimPoints[i]    = edgeDir * viewDistance;
        }

        // ── Rim circle ──
        rimRenderer = MakeLineRenderer("_FovRim", loop: true);
        rimRenderer.positionCount = rimSegs;
        rimRenderer.SetPositions(rimPoints);

        // ── Spokes from apex (local origin) to rim ──
        for (int i = 0; i < spokeCount; i++)
        {
            int rimIdx        = rimSegs / spokeCount * i;
            LineRenderer spoke = MakeLineRenderer($"_FovSpoke_{i}", loop: false);
            spoke.positionCount = 2;
            spoke.SetPosition(0, Vector3.zero);    // apex = camera origin
            spoke.SetPosition(1, rimPoints[rimIdx]);
            spokeRenderers.Add(spoke);
        }

        SetVisualizationColor(idleColor);
    }

    LineRenderer MakeLineRenderer(string childName, bool loop)
    {
        GameObject obj = new GameObject(childName);
        Transform  parentTransform = (cameraHead != null) ? cameraHead : transform;
        obj.transform.SetParent(parentTransform, false); // Attach to the rotating head
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale    = Vector3.one;

        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace  = false;   // positions are in local/parent space
        lr.loop           = loop;
        lr.startWidth     = lineWidth;
        lr.endWidth       = lineWidth;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows    = false;
        lr.generateLightingData = false;

        // "Sprites/Default" is natively transparent and supports vertex colours
        // in ALL Unity render pipelines (Built-in, URP, HDRP).
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader == null)
        {
            // Ultimate fallback: works in Built-in pipeline
            spriteShader = Shader.Find("Unlit/Color");
            Debug.LogWarning("[SecurityCamera] 'Sprites/Default' shader not found, falling back to Unlit/Color.");
        }
        lr.material = new Material(spriteShader);

        return lr;
    }

    void SetVisualizationColor(Color color)
    {
        if (rimRenderer != null)
        {
            rimRenderer.startColor = color;
            rimRenderer.endColor   = color;
        }
        foreach (LineRenderer s in spokeRenderers)
        {
            if (s == null) continue;
            s.startColor = color;
            s.endColor   = color;
        }
    }

    void SetVisualizationEnabled(bool enabled)
    {
        if (rimRenderer != null)
            rimRenderer.enabled = enabled;

        foreach (LineRenderer s in spokeRenderers)
        {
            if (s != null)
                s.enabled = enabled;
        }
    }

    void OnDisable()
    {
        SetVisualizationEnabled(false);
    }

    void OnEnable()
    {
        SetVisualizationEnabled(true);
    }

    // ── Editor Gizmos ──────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Transform viewSource = (cameraHead != null) ? cameraHead : transform;
        float    halfFOV   = fieldOfViewAngle * 0.5f;
        Color    wireColor = isPlayerInView ? Color.red : Color.yellow;
        Vector3  baseEdge  = Quaternion.AngleAxis(halfFOV, viewSource.right) * viewSource.forward;

        int       rimSegs  = 32;
        Vector3[] rimPts   = new Vector3[rimSegs];

        Gizmos.color = wireColor;
        for (int i = 0; i < rimSegs; i++)
        {
            float az      = 360f / rimSegs * i;
            Vector3 dir   = Quaternion.AngleAxis(az, viewSource.forward) * baseEdge;
            rimPts[i]     = viewSource.position + dir * viewDistance;
            Gizmos.DrawLine(viewSource.position, rimPts[i]);
        }
        for (int i = 0; i < rimSegs; i++)
            Gizmos.DrawLine(rimPts[i], rimPts[(i + 1) % rimSegs]);

        Gizmos.DrawRay(viewSource.position, (Quaternion.AngleAxis( halfFOV, viewSource.right) * viewSource.forward) * viewDistance);
        Gizmos.DrawRay(viewSource.position, (Quaternion.AngleAxis(-halfFOV, viewSource.right) * viewSource.forward) * viewDistance);
        Gizmos.DrawRay(viewSource.position, (Quaternion.AngleAxis( halfFOV, viewSource.up)    * viewSource.forward) * viewDistance);
        Gizmos.DrawRay(viewSource.position, (Quaternion.AngleAxis(-halfFOV, viewSource.up)    * viewSource.forward) * viewDistance);

        Gizmos.color = Color.white;
        Gizmos.DrawRay(viewSource.position, viewSource.forward * viewDistance);

        //Gizmos.color = hasAlerted ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(viewSource.position, 0.3f);
    }
#endif
}
