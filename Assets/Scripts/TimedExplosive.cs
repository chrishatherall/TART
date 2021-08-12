using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Causes an explosion after the time runs out

[RequireComponent(typeof(AudioSource))]
public class TimedExplosive : MonoBehaviour
{
    public float sTimeUntilExplosion;

    public float explosionRadius;
    // Explosion damage and force scale over radius
    public int explosionDamage;
    public float explosionForce;

    public GameObject physicalObject;
    public AudioClip explosionSound;
    public GameObject psGameobject;

    bool hasExploded = false;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (hasExploded) return;

        sTimeUntilExplosion -= Time.deltaTime;
        if (sTimeUntilExplosion < 0f) Explode();
    }

    void Explode()
    {
        hasExploded = true;

        // Find audio source and play audio clip
        AudioSource AS = GetComponent<AudioSource>();
        if (AS && explosionSound) AS.PlayOneShot(explosionSound);

        // Turn on the gameobject containing the particle emitters
        if (psGameobject) psGameobject.SetActive(true);

        // Deal damage to players within the radius
        Collider[] hitColliders = Physics.OverlapSphere(this.transform.position, explosionRadius);
        foreach(Collider collider in hitColliders)
        {
            BodyPart bp = collider.GetComponent<BodyPart>();
            if (bp)
            {
                // TODO raycast to each bodypart and see if it's behind cover
                // TODO damage isn't scaled
                bp.TakeDamage(explosionDamage, Vector3.Normalize(collider.transform.position - this.transform.position) * explosionForce);
            }
        }

        // Destroy the physical object that exploded
        if (physicalObject) Destroy(physicalObject);
        // Destroy rigidbody so our explosion doesn't run awau
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb) Destroy(rb);

        // Destroy self
        // TODO will throw errors if a photon thing?
        Destroy(this.gameObject, 10000);
    }
}
