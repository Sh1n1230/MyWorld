using UnityEngine;

public class Visuals3rdPerson : MonoBehaviour
{
    
    public CharacterControllerBase characterController;

    // Update is called once per frame
    void Update() {
        float dTime = Time.deltaTime;
        
        Vector3 moveVector = characterController.GetMoveVector();
        
        // rotate the visuals to match the move vector
        if (moveVector.sqrMagnitude > 0.1f) {
            Vector3 lookVector = moveVector;
            lookVector.y = 0f;
            Quaternion lookRotation = Quaternion.LookRotation(lookVector, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, lookRotation, dTime * 10f);
        }
    }
}
