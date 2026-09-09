using UnityEngine;

public class CigaretteSmokeController : MonoBehaviour
{
    [SerializeField] private ParticleSystem mouthSmoke;
    [SerializeField] private ParticleSystem cigaretteTipSmoke;

    public void StartMouthSmoke()
    {
        if (mouthSmoke != null) mouthSmoke.Play();
    }

    public void StopMouthSmoke()
    {
        if (mouthSmoke != null) mouthSmoke.Stop();
    }

    public void EmitMouthSmoke(int count)
    {
        if (mouthSmoke != null) mouthSmoke.Emit(count);
    }

    public void SetTipSmokeActive(bool isActive)
    {
        if (cigaretteTipSmoke == null) return;
        if (isActive) cigaretteTipSmoke.Play();
        else cigaretteTipSmoke.Stop();
    }
}