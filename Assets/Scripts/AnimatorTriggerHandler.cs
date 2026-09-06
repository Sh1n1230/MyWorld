using UnityEngine;
public class AnimatorTriggerHandler : MonoBehaviour
{
    public Animator animator;
    public void FireTrigger(string triggerName)
    {
        animator?.SetTrigger(triggerName);
        Debug.Log($"Trigger fired: {triggerName}");
    }
}