using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;
using static LogManager;

// Causes an explosion after the time runs out

[RequireComponent(typeof(AudioSource))]
public class TimedExplosive : MonoBehaviourPun
{
    readonly string logSrc = "T_EXPL";

    public float sTimeUntilExplosion;

    // Time after explosion until this object is remove entirely. Should be longer than the post-explosion visual effects.
    public float sPostExplosionDecayTime = 5;
    private float sTimeSinceExplosion;

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
        if (hasExploded)
        {
            // When we reach our post-explosion decay time, remove this object
            sTimeSinceExplosion += Time.deltaTime;
            if (photonView.IsMine && sTimeSinceExplosion > sPostExplosionDecayTime) PhotonNetwork.Destroy(this.photonView);
        } else
        {
            // When we reach our explosion timer, explode
            sTimeUntilExplosion -= Time.deltaTime;
            if (sTimeUntilExplosion < 0f) Explode();
        }

    }

    void Explode()
    {
        hasExploded = true;

        // Find audio source and play audio clip
        AudioSource AS = GetComponent<AudioSource>();
        if (AS && explosionSound) AS.PlayOneShot(explosionSound);

        // Turn on the gameobject containing the particle emitters
        if (psGameobject) psGameobject.SetActive(true);

        // Damage and force code should be done by the server
        if (PhotonNetwork.IsMasterClient)
        {
            // Group damage by player and send msg directly to player instead of bodypart, as we can affect multiple bodyparts at once
            Hashtable hitPlayers = new Hashtable();
            // Deal damage to players within the radius
            Collider[] hitColliders = Physics.OverlapSphere(this.transform.position, explosionRadius);
            foreach(Collider collider in hitColliders)
            {
                BodyPart bp = collider.GetComponent<BodyPart>();
                if (bp)
                {
                    if (!hitPlayers.ContainsKey(bp.p.ID)) hitPlayers.Add(bp.p.ID, "");
                    hitPlayers[bp.p.ID] += "/" + bp.name;
                }
            }

            lm.Log(logSrc, $"Explosion at {this.transform.position} with {explosionRadius} radius hit {hitColliders.Length} colliders and {hitPlayers.Count} players.");
        
            foreach (DictionaryEntry de in hitPlayers)
            {
                Player p = gm.GetPlayerByID(int.Parse(de.Key.ToString()));
                // Scale damage and force by distance
                float distance = Vector3.Distance(p.transform.position, this.transform.position);
                // In some cases, our distance can be more than the explosion radius if our colliders clip the edge of the explosion. In this case, ignore the damage 
                // because it'll otherwise become negative and do healing.
                if (distance > explosionRadius) break;
                int damage = Mathf.RoundToInt(explosionDamage * (1 - distance / explosionRadius));
                float force = explosionForce * (1 - distance / explosionRadius);
                if (p) p.photonView.RPC("DamageBone", Photon.Pun.RpcTarget.All, de.Value.ToString(), damage, Vector3.Normalize(p.transform.position + new Vector3(0f, 1f, 0f) - this.transform.position) * force, -1); // Note: use roughly the chest of the player so they are thrown upwards
            }
        }

        // Destroy the physical object that exploded
        if (physicalObject) Destroy(physicalObject);
        // Disable rigidbody so our explosion doesn't run away
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;
    }
}
