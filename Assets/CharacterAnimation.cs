using UnityEngine;
using UnityEngine.InputSystem;
public class AnimationTest : MonoBehaviour
{
    private Animator anim;
    void Start() { anim = GetComponent<Animator>(); }
    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        anim.SetFloat("Speed", kb.wKey.isPressed ? 1.0f : 0.0f);
        if (kb.spaceKey.wasPressedThisFrame) anim.SetTrigger("Jump");
    }
}