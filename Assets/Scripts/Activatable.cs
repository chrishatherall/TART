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

    public string requiredKey;

    // TODO add SyncVar values for when the activatable is "ready" and it's state
    // eg, buttons can be on/off or just push, levers are the same. They could also be one-use
    // These values would change the UI text overlay or disable the highlight entirely


    public void Start()
    {
        // Our controllable should be ourselves if nothing is defined.
        if (!targetControllable) targetControllable = this.gameObject;
    }

    [PunRPC]
    public void Activate(Vector3 position)
    {
        // TODO might want to do a beep or something here.

        // Actual activation (like doing STUFF) should only happen on the server.
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;

        // TODO check things like enabled/disabled, X role only, cooldown, bool on/off like levers, etc
        // Tell other scripts on this object to activate
        gm.Log("Activated " + nickname);
        // TODO how does this work with rpc? Maybe let controllables handle that?
        if (targetControllable != null)
        {
            targetControllable.SendMessage("OnActivated", position);
        }
        else
        {
            gameObject.SendMessage("OnActivated", position);
        }
    }
}
