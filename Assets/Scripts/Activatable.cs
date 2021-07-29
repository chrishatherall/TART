using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using static GameManager;

// An activatable is something that can be E'd, by a player and is highlighted when they look at it.
// Examples: Buttons, levers, doors
// When E'd, it broadcasts "OnActivated" on the target gameobject to be controlled. (This object if null)
public class Activatable : MonoBehaviourPun
{
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
        this.gameObject.AddComponent<PhotonView>();
    }

    [PunRPC]
    public void ActivateOnServer(Vector3 position, PhotonMessageInfo info)
    {
        // Tell other scripts on this object to activate
        gm.Log("Activated " + nickname);
        targetControllable.SendMessage("OnActivated", position);
    }

    // Called locally when a player activates this object
    public void Activate(Vector3 position)
    {
        // TODO check things like enabled/disabled, X role only, cooldown, bool on/off like levers, etc

        // This sends the activate to the server only, which could be a bit laggy for us and other clients
        this.photonView.RPC("Activate", RpcTarget.MasterClient, position);
    }
}
