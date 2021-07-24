using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class UI_devOverlay : MonoBehaviour
{
    public GameObject ui_container;

    private Text text;

    // Start is called before the first frame update
    void Start()
    {
        DontDestroyOnLoad(this);
        if (!ui_container)
        {
            Debug.LogError("[UI_devOverlay] No ui_container selected.");
            this.gameObject.SetActive(false);
            return;
        }
        text = GetComponentInChildren<Text>();
    }

    // Update is called once per frame
    void Update()
    {
        // Display the UI if Tab is being pressed.
        ui_container.SetActive(Input.GetKey(KeyCode.Tab));

        // Don't bother updating the ui if it's not being shown.
        if (!ui_container.active) return;

        string info = "";

        // Try to find a GM.
        GameManager GM = GameManager.gm;
        if (GM) {
            // Room code
            info += "Room code: " + PhotonNetwork.CurrentRoom.Name + "\n";
            // Game state
            info += "Game mode/state: " + PhotonNetwork.CurrentRoom.CustomProperties["gamemode"] + ":" + GM.gameState + "\n";
            // Player info
            GM.players.ForEach(delegate (Player p)
            {
                info += "[" + p.id + ":" + p.nickname + "]\t" + p.Role.Name + "\toil/damage:" + p.oil + "/" + p.damage + "\t\tisDead:" + p.isDead + "\tready:" + p.isReady + "\n";
            });
            info += "\nPre-round time: " + GM.preRoundTime;

        } else {
            info += "No GameManager found.\n";
        }

        info += "\n";

        // Try to find a network manager.
        //NetworkManager NM = FindObjectOfType<NetworkManager>();
        //if (NM) {
        //    info += "Server address: " + NM.networkAddress + "\n";
        //    info += "Active: " + NM.isNetworkActive + "\n";
        //} else {
        //    info += "No Network Manager found.\n";
        //}

        text.text = info;
    }
}
