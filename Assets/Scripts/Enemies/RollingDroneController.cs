using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public sealed class RollingDroneController : MonoBehaviour, IDamageable
{
    private enum DroneState
    {
        Wandering,
        Chasing,
        Fusing,
        Dead
    }

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float targetMemoryDuration = 5f;

    [Header("Vision")]
    [SerializeField, Min(0f)] private float visionRange = 12f;
    [SerializeField, Range(0f, 360f)] private float visionAngle = 120f;
    [SerializeField, Min(0f)] private float visionOriginHeight = 0.15f;
    [SerializeField, Min(0f)] private float targetSightHeight = 0.9f;
    [SerializeField] private LayerMask visionLayers = Physics.AllLayers;
    [SerializeField] private bool showVisionRange;
    [SerializeField] private Material visionRangeMaterial;
    [SerializeField, Range(8, 128)] private int visionRangeSegments = 48;

    [Header("Rolling Movement")]
    [SerializeField, Min(0f)] private float maxSpeed = 5f;
    [SerializeField, Min(0f)] private float maxAcceleration = 16f;
    [SerializeField, Min(0.05f)] private float velocityResponseTime = 0.45f;
    [SerializeField, Min(0f)] private float linearDamping = 0.2f;

    [Header("Wandering")]
    [SerializeField, Min(0f)] private float wanderRadius = 8f;
    [SerializeField, Min(0.1f)] private float waypointArrivalDistance = 1.1f;
    [SerializeField, Min(0f)] private float wanderPauseMin = 0.5f;
    [SerializeField, Min(0f)] private float wanderPauseMax = 1.5f;

    [Header("Explosion")]
    [SerializeField, Min(0f)] private float fuseStartDistance = 2.5f;
    [SerializeField, Min(0f)] private float fuseCancelDistance = 3f;
    [SerializeField, Min(0.1f)] private float fuseDuration = 2f;
    [SerializeField, Min(0f)] private float health = 100f;
    [SerializeField, Min(0f)] private float destroyDelayAfterExplosion = 0.2f;
    [SerializeField] private ExplosionHitbox explosionHitbox;

    private Rigidbody body;
    private SphereCollider bodyCollider;
    private DroneState state;
    private Vector3 wanderCenter;
    private Vector3 wanderDestination;
    private Vector3 lastKnownTargetPosition;
    private Vector3 logicalForward;
    private float lastSeenTime = float.NegativeInfinity;
    private float fuseTimer;
    private float wanderPauseTimer;
    private bool isWaitingAtWaypoint;
    private bool canCurrentlySeeTarget;
    private bool isAlive = true;
    private bool hasDetonated;
    private GameObject visionRangeVisual;
    private MeshFilter visionRangeMeshFilter;
    private MeshRenderer visionRangeMeshRenderer;
    private Mesh visionRangeMesh;
    private float builtVisionRange = -1f;
    private float builtVisionAngle = -1f;
    private int builtVisionSegments = -1;

    public bool IsAlive => isAlive;
    public float CurrentHealth => health;
    public bool IsFuseActive => state == DroneState.Fusing;
    public bool HasDetonated => hasDetonated;
    public float FuseDuration => Mathf.Max(0f, fuseDuration);
    public float FuseTimeRemaining => hasDetonated
        ? 0f
        : IsFuseActive ? Mathf.Max(0f, FuseDuration - fuseTimer) : FuseDuration;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<SphereCollider>();

        body.useGravity = true;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearDamping = Mathf.Max(0f, linearDamping);
        body.maxAngularVelocity = 20f;

        wanderCenter = body.position;
        logicalForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (logicalForward.sqrMagnitude < 0.001f)
            logicalForward = Vector3.forward;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        if (explosionHitbox == null)
            explosionHitbox = GetComponentInChildren<ExplosionHitbox>(true);

        if (explosionHitbox == null)
            Debug.LogError("RollingDroneController needs an ExplosionHitbox child.", this);

        ChooseWanderDestination();
        state = DroneState.Wandering;
    }

    private void Update()
    {
        if (state == DroneState.Dead)
            return;

        if (target == null)
        {
            CancelFuse();
            state = DroneState.Wandering;
            UpdateWandering();
            return;
        }

        canCurrentlySeeTarget = CanSeeTarget();
        if (canCurrentlySeeTarget)
        {
            lastKnownTargetPosition = target.position;
            lastSeenTime = Time.time;
            if (state == DroneState.Wandering)
            {
                state = DroneState.Chasing;
                isWaitingAtWaypoint = false;
            }
        }

        if (state == DroneState.Wandering)
        {
            UpdateWandering();
            return;
        }

        float targetDistance = HorizontalDistance(body.position, target.position);
        if (state == DroneState.Fusing)
        {
            if (targetDistance > Mathf.Max(fuseStartDistance, fuseCancelDistance))
            {
                CancelFuse();
                state = DroneState.Chasing;
            }
            else
            {
                fuseTimer += Time.deltaTime;
                if (fuseTimer >= fuseDuration)
                    Detonate();
            }

            return;
        }

        if (!canCurrentlySeeTarget && Time.time - lastSeenTime >= targetMemoryDuration)
        {
            state = DroneState.Wandering;
            isWaitingAtWaypoint = false;
            ChooseWanderDestination();
            UpdateWandering();
            return;
        }

        if (targetDistance <= fuseStartDistance)
            BeginFuse();
    }

    private void LateUpdate()
    {
        UpdateVisionRangeVisual();
    }

    private void FixedUpdate()
    {
        if (body == null || state == DroneState.Dead)
            return;

        Vector3 desiredVelocity = Vector3.zero;
        switch (state)
        {
            case DroneState.Wandering:
                if (!isWaitingAtWaypoint)
                    desiredVelocity = GetDesiredVelocity(wanderDestination);
                break;

            case DroneState.Chasing:
                Vector3 chaseDestination = Time.time - lastSeenTime < targetMemoryDuration
                    ? (target != null && canCurrentlySeeTarget ? target.position : lastKnownTargetPosition)
                    : wanderDestination;
                desiredVelocity = GetDesiredVelocity(chaseDestination);
                break;

            case DroneState.Fusing:
                desiredVelocity = Vector3.zero;
                break;
        }

        ApplyVelocitySteering(desiredVelocity);
    }

    public void TakeDamage(float amount)
    {
        if (!isAlive || amount <= 0f)
            return;

        health = Mathf.Max(0f, health - amount);
        if (health <= 0f)
            DieWithoutExplosion();
    }

    private bool CanSeeTarget()
    {
        if (target == null)
            return false;

        Vector3 toTarget = target.position - body.position;
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(toTarget, Vector3.up);
        float horizontalDistance = horizontalDirection.magnitude;
        if (horizontalDistance > visionRange || horizontalDistance <= 0.001f)
            return false;

        Vector3 forward = logicalForward.sqrMagnitude > 0.001f
            ? logicalForward
            : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        float halfAngle = Mathf.Clamp(visionAngle, 0f, 360f) * 0.5f;
        if (Vector3.Angle(forward, horizontalDirection / horizontalDistance) > halfAngle)
            return false;

        Vector3 origin = body.position + Vector3.up * visionOriginHeight;
        Vector3 targetPoint = target.position + Vector3.up * targetSightHeight;
        Vector3 ray = targetPoint - origin;
        float rayDistance = ray.magnitude;
        if (rayDistance <= 0.001f)
            return true;

        if (!Physics.Raycast(
                origin,
                ray / rayDistance,
                out RaycastHit hit,
                rayDistance,
                visionLayers,
                QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        return hit.transform == target || hit.transform.IsChildOf(target);
    }

    private void UpdateWandering()
    {
        if (isWaitingAtWaypoint)
        {
            wanderPauseTimer -= Time.deltaTime;
            if (wanderPauseTimer <= 0f)
            {
                isWaitingAtWaypoint = false;
                ChooseWanderDestination();
            }

            return;
        }

        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
        if (HorizontalDistance(body.position, wanderDestination) <= waypointArrivalDistance &&
            horizontalVelocity.magnitude <= 0.6f)
        {
            isWaitingAtWaypoint = true;
            wanderPauseTimer = Random.Range(wanderPauseMin, Mathf.Max(wanderPauseMin, wanderPauseMax));
        }
    }

    private void ChooseWanderDestination()
    {
        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        wanderDestination = new Vector3(
            wanderCenter.x + offset.x,
            body != null ? body.position.y : transform.position.y,
            wanderCenter.z + offset.y);
    }

    private Vector3 GetDesiredVelocity(Vector3 destination)
    {
        Vector3 offset = Vector3.ProjectOnPlane(destination - body.position, Vector3.up);
        if (offset.sqrMagnitude <= waypointArrivalDistance * waypointArrivalDistance)
            return Vector3.zero;

        return offset.normalized * maxSpeed;
    }

    private void ApplyVelocitySteering(Vector3 desiredVelocity)
    {
        Vector3 currentVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
        Vector3 velocityError = desiredVelocity - currentVelocity;
        Vector3 acceleration = velocityError / Mathf.Max(0.05f, velocityResponseTime);
        acceleration = Vector3.ClampMagnitude(acceleration, maxAcceleration);
        body.AddForce(acceleration, ForceMode.Acceleration);

        Vector3 heading = currentVelocity.sqrMagnitude > 0.04f
            ? currentVelocity
            : desiredVelocity;
        heading = Vector3.ProjectOnPlane(heading, Vector3.up);
        if (heading.sqrMagnitude > 0.04f)
            logicalForward = heading.normalized;
    }

    private void UpdateVisionRangeVisual()
    {
        if (!showVisionRange || visionRangeMaterial == null)
        {
            if (visionRangeMeshRenderer != null)
                visionRangeMeshRenderer.enabled = false;

            return;
        }

        EnsureVisionRangeMesh();
        EnsureVisionRangeVisual();

        visionRangeMeshFilter.sharedMesh = visionRangeMesh;
        visionRangeMeshRenderer.sharedMaterial = visionRangeMaterial;
        visionRangeMeshRenderer.enabled = true;

        Vector3 heading = logicalForward.sqrMagnitude > 0.001f
            ? logicalForward
            : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (heading.sqrMagnitude < 0.001f)
            heading = Vector3.forward;

        visionRangeVisual.transform.position = GetVisionRangeVisualPosition();
        visionRangeVisual.transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
    }

    private void EnsureVisionRangeVisual()
    {
        if (visionRangeVisual != null)
            return;

        visionRangeVisual = new GameObject("VisionRangeVisual");
        visionRangeVisual.transform.SetParent(transform, false);
        visionRangeMeshFilter = visionRangeVisual.AddComponent<MeshFilter>();
        visionRangeMeshRenderer = visionRangeVisual.AddComponent<MeshRenderer>();
        visionRangeMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        visionRangeMeshRenderer.receiveShadows = false;
    }

    private void EnsureVisionRangeMesh()
    {
        float range = Mathf.Max(0f, visionRange);
        float angle = Mathf.Clamp(visionAngle, 0f, 360f);
        int segments = Mathf.Clamp(visionRangeSegments, 8, 128);

        if (visionRangeMesh == null)
        {
            visionRangeMesh = new Mesh { name = "Drone Vision Range" };
            visionRangeMesh.hideFlags = HideFlags.DontSave;
        }

        if (Mathf.Approximately(range, builtVisionRange) &&
            Mathf.Approximately(angle, builtVisionAngle) &&
            segments == builtVisionSegments)
        {
            return;
        }

        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;

        float halfAngleRadians = angle * 0.5f * Mathf.Deg2Rad;
        for (int i = 0; i <= segments; i++)
        {
            float angleRadians = Mathf.Lerp(-halfAngleRadians, halfAngleRadians, i / (float)segments);
            vertices[i + 1] = new Vector3(
                Mathf.Sin(angleRadians) * range,
                0f,
                Mathf.Cos(angleRadians) * range);

            if (i < segments)
            {
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i + 2;
            }
        }

        visionRangeMesh.Clear();
        visionRangeMesh.vertices = vertices;
        visionRangeMesh.triangles = triangles;
        visionRangeMesh.RecalculateNormals();
        visionRangeMesh.RecalculateBounds();

        builtVisionRange = range;
        builtVisionAngle = angle;
        builtVisionSegments = segments;
    }

    private Vector3 GetVisionRangeVisualPosition()
    {
        Vector3 center = body != null ? body.position : transform.position;
        float radius = bodyCollider != null ? bodyCollider.radius : 0.5f;
        return center - Vector3.up * (radius - 0.02f);
    }

    private void OnDrawGizmos()
    {
        if (!showVisionRange)
            return;

        EnsureVisionRangeMesh();

        Vector3 heading = Application.isPlaying && logicalForward.sqrMagnitude > 0.001f
            ? logicalForward
            : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (heading.sqrMagnitude < 0.001f)
            heading = Vector3.forward;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = Matrix4x4.TRS(
            GetVisionRangeVisualPosition(),
            Quaternion.LookRotation(heading, Vector3.up),
            Vector3.one);
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawMesh(visionRangeMesh);
        Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
        Gizmos.DrawWireMesh(visionRangeMesh);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private void BeginFuse()
    {
        state = DroneState.Fusing;
        fuseTimer = 0f;
    }

    private void CancelFuse()
    {
        fuseTimer = 0f;
    }

    private void Detonate()
    {
        if (!isAlive || hasDetonated)
            return;

        hasDetonated = true;
        isAlive = false;
        state = DroneState.Dead;

        if (explosionHitbox != null)
            explosionHitbox.Activate(this, transform);
        else
            Debug.LogError("The drone cannot detonate because its ExplosionHitbox is missing.", this);

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        bodyCollider.enabled = false;
        float destructionDelay = explosionHitbox != null
            ? Mathf.Max(destroyDelayAfterExplosion, explosionHitbox.ActiveDuration + 0.05f)
            : destroyDelayAfterExplosion;
        Destroy(gameObject, destructionDelay);
    }

    private void DieWithoutExplosion()
    {
        if (!isAlive)
            return;

        isAlive = false;
        state = DroneState.Dead;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        bodyCollider.enabled = false;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (visionRangeMesh != null)
        {
            if (Application.isPlaying)
                Destroy(visionRangeMesh);
            else
                DestroyImmediate(visionRangeMesh);
        }

        if (visionRangeVisual != null)
        {
            if (Application.isPlaying)
                Destroy(visionRangeVisual);
            else
                DestroyImmediate(visionRangeVisual);
        }
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        Vector3 difference = Vector3.ProjectOnPlane(first - second, Vector3.up);
        return difference.magnitude;
    }
}
