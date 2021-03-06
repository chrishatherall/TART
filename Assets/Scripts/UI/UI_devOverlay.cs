using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using static GameManager;
using static LogManager;

public class UI_devOverlay : MonoBehaviour
{
    readonly string logSrc = "UI_DEV";

    public GameObject ui_container;

    private Text text;

    // Start is called before the first frame update
    void Start()
    {
        DontDestroyOnLoad(this);
        if (!ui_container)
        {
            lm.LogError(logSrc,"No ui_container selected.");
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

        // Room code
        info += "Room code: " + PhotonNetwork.CurrentRoom.Name + "\n";
        // Game state
        info += "Game mode/state: " + gm.gamemode + ":" + gm.CurrentGameState + "\n";
        // Player info
        gm.characters.ForEach(delegate (Character p)
        {
            info += "[" + p.ID + ":" + p.nickname + "]\t" + p.Role.Name + "\toil/damage:" + p.oil + "/" + p.GetDamage() + "\t\tisDead:" + p.IsDead + "\tready:" + p.isReady + "\n";
        });

        info += "\n";

        text.text = info;
    }
}
