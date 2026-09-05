using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    
    public Transform target;
    public Vector3 targetOffsetHigh = new Vector3(0,3,0);
    public Vector3 targetOffsetLow = new Vector3(0,1,0);
    public float cameraXYOffset = 5f;
    public float cameraYOffset = 5f;
    public float lerpSpeed = 5f;

    [Header("Collision")]
    // Keeps the camera out of walls and ceilings in tight interiors.
    public bool collisionEnabled = true;
    // Layers that can block the camera. The target's own colliders are always skipped.
    public LayerMask collisionMask = ~0;
    // Fattens the cast so the camera stops before the near clip plane enters geometry.
    public float collisionRadius = 0.3f;
    // Derives collisionRadius from the Camera's near clip plane and FOV instead.
    public bool radiusFromNearPlane = true;
    // Extra gap kept between the camera and whatever it hit.
    public float collisionSkin = 0.05f;
    // Never get closer to the pivot than this, even when fully boxed in.
    public float minDistance = 0.6f;
    // How fast the camera slides back out once the obstruction is gone (units/sec).
    public float returnSpeed = 6f;
    
    Vector3 targetVelocity = Vector3.zero; 
    Vector3 lookTarget = Vector3.zero;
    Vector3 lastTargetPosition = Vector3.zero;
    
    Vector3 camPos = Vector3.zero; 
    Vector3 lookVector = Vector3.zero;

    Camera cam;
    float currentDistance = -1f;
    readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        targetVelocity = Vector3.zero;
        lastTargetPosition = target.position;
        lookTarget = target.position + targetOffsetHigh;

        camPos = transform.position;
        cam = GetComponent<Camera>();
    }

    public void InputLookVector(Vector2 newLookVector) {
        lookVector = newLookVector;
    }

    // Update is called once per frame
    void LateUpdate() {
        
        // look the camera up and down
        cameraYOffset = Mathf.Clamp(cameraYOffset - lookVector.y * Time.deltaTime * 5.0f, 0f, 10f);
        
        Vector3 currentTargetOffset = Vector3.Lerp(targetOffsetLow, targetOffsetHigh, cameraYOffset * 0.1f);
        
        Vector3 targetPos = target.position;
        Vector3 newTargetVelocity = (targetPos - lastTargetPosition) / Time.deltaTime;
        targetVelocity = Vector3.Lerp(targetVelocity, newTargetVelocity, Time.deltaTime * 5.0f);
        lastTargetPosition = targetPos;
        
        Vector3 newLookTarget = targetPos + (targetVelocity * 0.25f) + currentTargetOffset;
        lookTarget = Vector3.Lerp(lookTarget, newLookTarget, Time.deltaTime * lerpSpeed);

        Vector3 targetCamPos = camPos;
        
        // look the camera left and right
        Vector3 cameraRight = transform.right;
        targetCamPos -= cameraRight * (lookVector.x * 2.0f);
        
        targetCamPos -= targetPos;
        targetCamPos.y = 0;
        // guard against a degenerate direction when the camera sits exactly above the target
        if (targetCamPos.sqrMagnitude < 0.0001f) targetCamPos = -transform.forward.normalized;
        targetCamPos = targetCamPos.normalized * cameraXYOffset;
        targetCamPos.y = cameraYOffset;
        targetCamPos += targetPos;
        
        
        camPos = Vector3.Lerp(camPos, targetCamPos, Time.deltaTime * lerpSpeed);
        
        // camPos stays the unobstructed "ideal" orbit position; collision only moves the transform,
        // so the camera slides straight back out once the wall is no longer in the way.
        transform.position = collisionEnabled ? ResolveCollision(targetPos + currentTargetOffset, camPos) : camPos;
        transform.LookAt(lookTarget);
    }

    // Sweeps a sphere from the character's head towards the ideal camera position and pulls
    // the camera in to the first thing it hits.
    Vector3 ResolveCollision(Vector3 pivot, Vector3 desiredPos) {
        Vector3 dir = desiredPos - pivot;
        float desiredDistance = dir.magnitude;
        if (desiredDistance < 0.0001f) return desiredPos;
        dir /= desiredDistance;

        float radius = radiusFromNearPlane && cam != null ? NearPlaneRadius() : collisionRadius;
        float allowed = desiredDistance;

        int count = Physics.SphereCastNonAlloc(pivot, radius, dir, hitBuffer,
                                               desiredDistance, collisionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++) {
            RaycastHit hit = hitBuffer[i];
            // distance 0 means the sweep started already overlapping this collider - no usable normal
            if (hit.distance <= 0f) continue;
            if (IsPartOfTarget(hit.collider.transform)) continue;
            if (hit.distance < allowed) allowed = hit.distance;
        }

        if (allowed < desiredDistance) allowed = Mathf.Max(allowed - collisionSkin, minDistance);

        // snap inwards so we never clip through a wall, ease back outwards
        if (currentDistance < 0f || allowed < currentDistance) currentDistance = allowed;
        else currentDistance = Mathf.MoveTowards(currentDistance, allowed, returnSpeed * Time.deltaTime);

        return pivot + dir * currentDistance;
    }

    // Half-diagonal of the near clip plane, so the whole plane clears geometry rather than just its centre.
    float NearPlaneRadius() {
        float halfHeight = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.nearClipPlane;
        float halfWidth = halfHeight * cam.aspect;
        return Mathf.Sqrt(halfHeight * halfHeight + halfWidth * halfWidth) + collisionSkin;
    }

    bool IsPartOfTarget(Transform t) {
        return target != null && t.IsChildOf(target);
    }
}
