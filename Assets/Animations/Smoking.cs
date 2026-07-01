using UnityEngine;
using UnityEngine.InputSystem;
public class SmokingController : MonoBehaviour
{
    public Animator animator;
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            animator.SetTrigger("Smoke");
        }
    }
}