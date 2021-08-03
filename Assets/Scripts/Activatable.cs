using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using static GameManager;
using static LogManager;

// An activatable is something that can be E'd, by a player and is highlighted when they look at it.
// Examples: Buttons, levers, doors
// When E'd, it broadcasts "OnActivated" on the target gameobject to be controlled. (This object if null)
[RequireComponent(typeof(PhotonView))]
public class Activatable : MonoBehaviourPun
{
    readonly string logSrc = "ACTIV";

    // GameObject controlled by this activatable. E.g, the trapdoor controlled by this lever
    public GameObject targetControllable;

    // Friendly name used in UI components
    public string nickname;

    // Temporary way of locking doors, will be removed in the future
    public string requiredKey;

    public void Start()
    {
        // Our controllable should be ourselves if nothing is defined.
        if (!targetControllable) targetControllable = this.gameObject;
    }

    [PunRPC]
    public void Activated(Vector3 position, PhotonMessageInfo info)
    {
        // Tell other scripts on this object to activate
        lm.Log(logSrc,"Activated " + nickname);
        targetControllable.SendMessage("OnActivated", position);
    }

    // Called locally when a player activates this object
    public void Activate(Vector3 position)
    {
        // TODO check things like enabled/disabled, X role only, cooldown, bool on/off like levers, etc

        this.photonView.RPC("Activated", RpcTarget.All, position);
    }
}
