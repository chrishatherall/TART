using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;

// Causes an explosion after the time runs out

[RequireComponent(typeof(AudioSource))]
public class TimedExplosive : MonoBehaviourPun
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
        if (!photonView.IsMine || hasExploded) return;

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

        // group damage by player and send msg directly to player instead of bodypart, as we can affect multiple bodyparts at once

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
        
        foreach (DictionaryEntry de in hitPlayers)
        {
            Player p = gm.GetPlayerByID(int.Parse(de.Key.ToString()));
            // Scale damage and force by distance
            float distance = Vector3.Distance(p.transform.position, this.transform.position);
            int damage = Mathf.RoundToInt(explosionDamage * (1 - distance / explosionRadius));
            float force = explosionForce * (distance / explosionRadius);
            if (p) p.photonView.RPC("TakeDamage", Photon.Pun.RpcTarget.All, damage, de.Value.ToString(), Vector3.Normalize(p.transform.position + new Vector3(0f, 1f, 0f) - this.transform.position) * force); // Note: use roughly the chest of the player so they are thrown upwards
        }

        // Destroy the physical object that exploded
        if (physicalObject) Destroy(physicalObject);
        // Destroy rigidbody so our explosion doesn't run away
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb) Destroy(rb);

        // TODO Destroy self
        //PhotonNetwork.Destroy(this.photonView, 10000);
    }
}
