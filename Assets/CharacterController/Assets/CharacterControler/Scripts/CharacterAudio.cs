using UnityEngine;

public class CharacterAudio : MonoBehaviour
{
    [SerializeField] AudioClip[] audioClipJumps;
    [SerializeField] AudioClip[] audioClipLands;
    [SerializeField] AudioClip[] audioClipStepsLeft;
    [SerializeField] AudioClip[] audioClipStepsRight;

    [SerializeField] AudioSource audioSource;

    bool altStep = false;
    public void PlayStep() {

        if (altStep) {
            audioSource.PlayOneShot(audioClipStepsLeft[Random.Range(0, audioClipStepsLeft.Length)]);
        } else {
            audioSource.PlayOneShot(audioClipStepsRight[Random.Range(0, audioClipStepsRight.Length)]);
        }

        altStep = !altStep;

    }
    
    public void PlayJump() {
        audioSource.PlayOneShot(audioClipJumps[Random.Range(0, audioClipJumps.Length)]);
    }
    
    public void PlayLand() {
        audioSource.PlayOneShot(audioClipLands[Random.Range(0, audioClipLands.Length)]);
    }
}
