using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    
    public Transform target;
    public Vector3 targetOffsetHigh = new Vector3(0,3,0);
    public Vector3 targetOffsetLow = new Vector3(0,1,0);
    public float cameraXYOffset = 5f;
    public float cameraYOffset = 5f;
    public float lerpSpeed = 5f;
    
    Vector3 targetVelocity = Vector3.zero; 
    Vector3 lookTarget = Vector3.zero;
    Vector3 lastTargetPosition = Vector3.zero;
    
    Vector3 camPos = Vector3.zero; 
    Vector3 lookVector = Vector3.zero;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        targetVelocity = Vector3.zero;
        lastTargetPosition = target.position;
        lookTarget = target.position + targetOffsetHigh;

        camPos = transform.position;
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
        targetCamPos = targetCamPos.normalized * cameraXYOffset;
        targetCamPos.y = cameraYOffset;
        targetCamPos += targetPos;
        
        
        camPos = Vector3.Lerp(camPos, targetCamPos, Time.deltaTime * lerpSpeed);
        
        transform.position = camPos;
        transform.LookAt(lookTarget);
    }
}
