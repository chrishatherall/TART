using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class enableForLocalNetworkPlayer : MonoBehaviourPun
{
    [SerializeField]
    Behaviour[] compsToEnable;

    // Start is called before the first frame update
    void Start()
    {
        if (photonView.IsMine)
        {
            for (int i = 0; i < compsToEnable.Length; i++)
            {
                compsToEnable[i].enabled = true;
            }
        }
    }
}
