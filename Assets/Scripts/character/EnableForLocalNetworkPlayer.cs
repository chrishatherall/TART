using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;


// Some components on a player prefab are disabled by default. These components need to
// be enabled for the local player only, which is facilitated by this script.
public class EnableForLocalNetworkPlayer : MonoBehaviourPun
{
    // List of components we need to enable.
    [SerializeField]
    Behaviour[] compsToEnable;

    [SerializeField] 
    GameObject[] GameobjectsToDisable;

    // Start is called before the first frame update
    void Start()
    {
        if (photonView.IsMine)
        {
            foreach (Behaviour b in compsToEnable)
            {
                b.enabled = true;
            }

            foreach (GameObject g in GameobjectsToDisable)
            {
                g.SetActive(false);
            }
        }
    }
}
