using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-1)]
public class CharacterInput1stPerson : MonoBehaviour
{
    public CharacterControllerBase characterController;
    public Transform cameraYaw;
    public Transform cameraPitch;
    
    private InputAction moveControl;
    private InputAction lookControl;
    private InputAction jumpAction;
    private InputAction mouseDelta;
    
    private InputCharacter inputCharacter;

    float yawRotation = 0f;
    float pitchRotation = 0f;
    
    void Awake() {
        inputCharacter = new InputCharacter();
    }

    void OnEnable() {
        inputCharacter.Enable();

        moveControl = inputCharacter.Character.Move;
        moveControl.Enable();
        
        lookControl = inputCharacter.Character.Look;
        lookControl.Enable();
        
        jumpAction = inputCharacter.Character.Jump;
        jumpAction.Enable();
        
        mouseDelta = inputCharacter.Character.MouseDelta;
        mouseDelta.Enable();
    }
    
    void OnDisable() {
        moveControl.Disable();
        lookControl.Disable();
        
        jumpAction.Disable();
        
        mouseDelta.Disable();

        inputCharacter.Disable();
    }

    void OnDestroy() {
        moveControl.Dispose();
        lookControl.Dispose();
        
        jumpAction.Dispose();
        
        mouseDelta.Dispose();
        
        inputCharacter.Dispose();
    }

    void Update() {

        Vector2 lookVector = lookControl.ReadValue<Vector2>();
        lookVector += mouseDelta.ReadValue<Vector2>() * 0.1f;
        
        yawRotation += lookVector.x * Time.deltaTime * 180.0f;
        if( yawRotation > 180.0f ) yawRotation -= 360.0f;
        if( yawRotation < -180.0f ) yawRotation += 360.0f;
        cameraYaw.localRotation = Quaternion.Euler( new Vector3(0f, yawRotation, 0f) );
        
        pitchRotation -= lookVector.y * Time.deltaTime * 180.0f;
        if( pitchRotation > 85f ) pitchRotation = 85f;
        if( pitchRotation < -85.0f ) pitchRotation = -85f;
        cameraPitch.localRotation = Quaternion.Euler( new Vector3(pitchRotation, 0f, 0f) );
        
        characterController.InputJump(jumpAction.WasPressedThisFrame(), jumpAction.IsPressed());
        characterController.InputMoveVector(GetWorldMoveVector());

    }
    
    private Vector2 GetMoveVector() {
        return moveControl.ReadValue<Vector2>();
    }

    private Vector3 GetWorldMoveVector() {

        Vector2 localInput = GetMoveVector();
        Vector3 worldInput = Vector3.zero;

        Vector3 localRight = cameraYaw.transform.right;
        //localRight.y = 0f;
        //localRight.Normalize();
        
        Vector3 localFarward = cameraYaw.transform.forward;
        //localFarward.y = 0f;
        //localFarward.Normalize();
        
        worldInput += localRight * localInput.x;
        worldInput += localFarward * localInput.y;
        
        return worldInput;
    }
}
