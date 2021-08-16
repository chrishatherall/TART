using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;

// Play material-appropriate sounds on collider event
public class NoiseOnHit : MonoBehaviour
{
    // Play sound for THIS material and HIT material.
    // Low volume for now, but later increase volume with hit velocity
    // Use footstep sound engine

    // Time when last sounds were played
    float lastPlayTime = 0f;
    // Cooldown between sounds
    [SerializeField]
    float sCooldown = 0.2f;

    // Ref to the local collider
    Collider thisCollider;
    // Ref to our audio source
    AudioSource audioSrc;

    // Start is called before the first frame update
    void Start()
    {
        thisCollider = GetComponent<Collider>();
        audioSrc = GetComponent<AudioSource>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!thisCollider || !audioSrc) return;
        if (Time.time < lastPlayTime + sCooldown) return;
        lastPlayTime = Time.time;

        Debug.Log(collision.collider.material.name);
        audioSrc.PlayOneShot(gm.GetFootstepByMaterial(collision.collider.material), 0.3f);
        audioSrc.PlayOneShot(gm.GetFootstepByMaterial(thisCollider.material), 0.3f);
    }
}
