using UnityEngine;

public class SmokeEmitter : MonoBehaviour
{
    [SerializeField] private ParticleSystem smokeParticle;

    public void EmitSmoke()
    {
        if (smokeParticle != null && !smokeParticle.isPlaying)
        {
            smokeParticle.Play();
        }
    }

    public void StopSmoke()
    {
        if (smokeParticle != null && smokeParticle.isPlaying)
        {
            smokeParticle.Stop();
        }
    }
}