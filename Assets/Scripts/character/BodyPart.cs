using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static LogManager;

// Tells a parent Player script when we're damaged
public class BodyPart : MonoBehaviour
{
    readonly string logSrc = "BodyPart";

    // Ref to our parent Player script
    Player p;
    // The damage on this body part. Note, this is set by the Player and NOT here, as
    // the TakeDamage script below is usually called on other clients, hence the RPC.
    [SerializeField]
    int damage;
    // The particle system we turn on when damaged. Needs to be manually set
    [SerializeField]
    ParticleSystem ps;

    public int Damage { 
        get => damage; 
        set => damage = value;
    }

    private void Update()
    {
        // Control particle system
        if (!ps) return;
        if (ps.gameObject.activeSelf)
        {
            // Find reasons to disable ps
            if (damage == 0 || p.oil == 0) ps.gameObject.SetActive(false);
        } else
        {
            // Find reasons to enable ps
            if (damage > 0 && p.oil > 0) ps.gameObject.SetActive(true);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        p = GetComponentInParent<Player>();
        // Warn if ps isn't set. Add bone name.
        if (!ps) lm.LogError(logSrc, "Missing particle system reference on " + this.name);
    }

    // This is an object that can receive damage
    public void TakeDamage(int dmg)
    {
        // Note: This method is always local to the damage dealer, we need to forward damage onto the player script via rpc.
        // Also sends bone (object) name, so the Player knows which part of the body took damage
        if (p) p.photonView.RPC("TakeDamage", Photon.Pun.RpcTarget.All, dmg, this.name);
    }

}
