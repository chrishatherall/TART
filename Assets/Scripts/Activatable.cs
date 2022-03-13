using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using static GameManager;
using static LogManager;

// An activatable is something that can be activated (by pressing E) by a player and is highlighted when they look at it.
// Examples: Buttons, levers, doors
// When activated, it broadcasts "OnActivated" on the target GameObject to be controlled.
[RequireComponent(typeof(PhotonView))]
public class Activatable : MonoBehaviourPun
{
    readonly string logSrc = "ACTIV";

    // GameObject controlled by this activatable. E.g, the trapdoor controlled by this lever
    public GameObject targetControllable;

    // Friendly name used in UI components
    public string nickname;

    // Activatables can be set to require a key in the player's inventory by populating this string.
    public string requiredKey;

    public void Start()
    {
        // Our controllable should be set to ourselves if nothing else is defined.
        if (!targetControllable) targetControllable = this.gameObject;
    }

    // Called by Activate on a client
    [PunRPC]
    public void Activated(Vector3 position, PhotonMessageInfo info)
    {
        // Tell other scripts on this object to activate
        lm.Log(logSrc,"Activated " + nickname);
        targetControllable.SendMessage("OnActivated", position, SendMessageOptions.DontRequireReceiver);
    }

    // Called locally when a player activates this object
    public void Activate(Vector3 position)
    {
        // TODO check activation requirements like enabled/disabled, X role only, cooldown, bool on/off like levers, etc

        this.photonView.RPC("Activated", RpcTarget.All, position);
    }
}
