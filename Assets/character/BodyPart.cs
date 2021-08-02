using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Tells a parent Player script when we're damaged
public class BodyPart : MonoBehaviour
{
    // Ref to our parent Player script
    Player p;
    // The damage on this body part. Note, this is set by the Player and NOT here, as
    // the TakeDamage script below is usually called on other clients, hence the RPC.
    public int damage;

    // Start is called before the first frame update
    void Start()
    {
        p = GetComponentInParent<Player>();    
    }

    // This is an object that can receive damage
    public void TakeDamage(int dmg)
    {
        // Note: This method is always local to the damage dealer, we need to forward damage onto the player script via rpc.
        // Also sends bone (object) name, so the Player knows which part of the body took damage
        if (p) p.photonView.RPC("TakeDamage", Photon.Pun.RpcTarget.All, dmg, this.name);
    }

}
