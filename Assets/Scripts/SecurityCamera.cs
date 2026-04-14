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

    [Header("Detection Settings")]
    [Tooltip("Full field of view in degrees. Camera faces its local +Z (blue arrow).")]
    public float fieldOfViewAngle = 100f;

    [Tooltip("Maximum detection range in Unity units.")]
    public float viewDistance = 15f;

    [Tooltip("Maximum vertical height difference (±) the camera can detect across.")]
    public float viewHeight = 3f;

    [Tooltip("Layers that block line of sight (walls, etc). Do NOT include the Player layer.")]
    public LayerMask obstructionMask;

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

    // Track the score at the time of the last alert so we detect each NEW steal separately.
    // Starts at -1 so the first steal (score goes from 0 → positive) always triggers.
    private int  lastAlertedScore = -1;
    private bool alertPending     = false; // true while the 5-second dispatch delay is running

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

        // Trigger on every new steal that happens while the player is in view.
        // We compare scores so each stolen item fires exactly once.
        if (isPlayerInView && playerController.score > lastAlertedScore && playerController.hasStolenSomething)
        {
            lastAlertedScore = playerController.score;
            TriggerAlert();
        }
    }

    // ── Detection ──────────────────────────────────────────────────────────────
    bool CheckPlayerInView()
    {
        Vector3 toPlayer = playerTransform.position - transform.position;

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

        // 3. Angle check
        float angle = Vector3.Angle(transform.forward, toPlayer);
        if (angle > fieldOfViewAngle * 0.5f)
        {
            if (showDebugLogs) Debug.Log($"[SecurityCamera] '{name}' FAIL: angle {angle:F1}° > halfFOV {fieldOfViewAngle * 0.5f}°");
            return false;
        }

        // 4. Line-of-sight raycast
        Vector3 eyePos    = transform.position;
        Vector3 targetPos = playerTransform.position + Vector3.up * 0.8f;
        Vector3 direction = (targetPos - eyePos).normalized;
        float   dist      = Vector3.Distance(eyePos, targetPos);

        if (Physics.Raycast(eyePos, direction, dist, obstructionMask))
        {
            if (showDebugLogs) Debug.Log($"[SecurityCamera] '{name}' FAIL: line-of-sight blocked.");
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
        StartCoroutine(DispatchGuardsDelayed(stealPosition, 5f));
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
        obj.transform.SetParent(transform, false); // false = keep local transform zeroed
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

    // ── Editor Gizmos ──────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        float    halfFOV   = fieldOfViewAngle * 0.5f;
        Color    wireColor = isPlayerInView ? Color.red : Color.yellow;
        Vector3  baseEdge  = Quaternion.AngleAxis(halfFOV, transform.right) * transform.forward;

        int       rimSegs  = 32;
        Vector3[] rimPts   = new Vector3[rimSegs];

        Gizmos.color = wireColor;
        for (int i = 0; i < rimSegs; i++)
        {
            float az      = 360f / rimSegs * i;
            Vector3 dir   = Quaternion.AngleAxis(az, transform.forward) * baseEdge;
            rimPts[i]     = transform.position + dir * viewDistance;
            Gizmos.DrawLine(transform.position, rimPts[i]);
        }
        for (int i = 0; i < rimSegs; i++)
            Gizmos.DrawLine(rimPts[i], rimPts[(i + 1) % rimSegs]);

        Gizmos.DrawRay(transform.position, (Quaternion.AngleAxis( halfFOV, transform.right) * transform.forward) * viewDistance);
        Gizmos.DrawRay(transform.position, (Quaternion.AngleAxis(-halfFOV, transform.right) * transform.forward) * viewDistance);
        Gizmos.DrawRay(transform.position, (Quaternion.AngleAxis( halfFOV, transform.up)    * transform.forward) * viewDistance);
        Gizmos.DrawRay(transform.position, (Quaternion.AngleAxis(-halfFOV, transform.up)    * transform.forward) * viewDistance);

        Gizmos.color = Color.white;
        Gizmos.DrawRay(transform.position, transform.forward * viewDistance);

        //Gizmos.color = hasAlerted ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
#endif
}
