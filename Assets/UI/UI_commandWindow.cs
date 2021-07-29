using Photon.Pun;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static GameManager;

public class UI_commandWindow : MonoBehaviourPun
{
    string[] allCommands;     // Concat if client+server commands
    string[] clientCommands = // Commands which can be run by all players
    {
        "KILL"
    };
    string[] serverCommands = // Commands run only by the server/host
    {
        "SPAWN",
        "ROUNDRESTART"
    };

    string[] spawnableItems = {
        "MACCERS",
        "AK47",
        "PISTOL",
        "PUMPO"
    };

    // The command window label we should enable/disable on ` or ESC
    [SerializeField]
    GameObject commandWindowObject;
    // The textbox where commands are entered.
    [SerializeField]
    InputField commandInput;
    // The text field inside the scrollable log window
    [SerializeField]
    Text logText;

    // Called when someone finishes entering text in the command field
    // Not just called when hitting enter. Sometimes when hitting ` or maybe even clicking off?
    public void CommandEntered()
    {
        // This method is called when we close the command window with `. This means a half-entered
        // command could be sent, which we don't want. So if a command contains `, remove it and 
        // ignore everything.
        if (commandInput.text.Contains("`"))
        {
            // Strip out backticks
            commandInput.text = commandInput.text.Replace("`", "");
            return;
        }
        // Ignore if 0-length
        if (commandInput.text.Length == 0) return;
        // Check for known command
        string command = commandInput.text.ToUpper().Split(' ')[0];
        if (!allCommands.Contains(command))
        {
            gm.Log($"[COMMAND] Unknown command: {command}.");
            return;
        }
        // Send command to server
        photonView.RPC("HandleCommand", RpcTarget.MasterClient, commandInput.text);
        // Re-focus the command window and clear
        commandInput.ActivateInputField();
        commandInput.text = "";
    }

    [PunRPC]
    public void HandleCommand (string command, PhotonMessageInfo pmi)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        gm.Log($"[COMMAND] {pmi.Sender.ActorNumber}:{pmi.Sender.NickName} entered [{commandInput.text}].");

        // Ensure all commands are in uppercase
        string[] split = command.ToUpper().Split(' ');

        // If the command is a server command, check it's from the master client
        if (serverCommands.Contains(split[0]) && !pmi.Sender.IsMasterClient)
        {
            gm.Log($"[COMMAND] Ignoring command [{commandInput.text}]. Not server.");
            return;
        }

        // Get player who sent the command
        Player p = gm.GetPlayerByActorNumber(pmi.Sender.ActorNumber);
        if (!p)
        {
            gm.LogError($"[COMMAND] Couldnt find player {pmi.Sender.ActorNumber}.");
            return;
        }

        // Command-specific logic
        switch (split[0])
        {
            case "SPAWN":
                // Check the item we want to spawn is legit
                if (spawnableItems.Contains(split[1]))
                {
                    p.gameObject.GetPhotonView().RPC("RpcDropItem", RpcTarget.MasterClient, split[1], false);
                } else
                {
                    gm.Log($"[COMMAND] Invalid SPAWN value: {split[1]}.");
                }
                break;

            case "KILL":
                // Currently you can only kill your own player
                if (pmi.Sender.IsLocal) p.Die();
                break;

            case "ROUNDRESTART":
                // Set round to postround
                gm.CurrentGameState = GameState.PostRound;
                // Set roundovertime to something smaller than maxroundovertime to make the postround quick
                gm.curPostRoundTime = gm.postRoundTime - 1;
                // This should initiate the postround cleanup, give it time, then start the preround spawning
                // TODO somehow force a draw with no points
                break;

            default:
                gm.Log($"[COMMAND] Invalid command: {split[0]}.");
                break;
        }
    }

    public void HandleLog(string message, bool isError)
    {
        logText.text += "\n" + message;
        // TODO colour errors in red
        // TODO scroll down automatically
    }

    public void HandleUnityLog(string logString, string stack, LogType type)
    {
        logText.text += "\n[UNITY] [" + type.ToString() + "] " + logString;
        if (type == LogType.Exception)
        {
            logText.text += "\n" + stack;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        if (!commandWindowObject || !commandInput || !logText)
        {
            Debug.LogError("UI_commandWindow is missing references");
            this.enabled = false;
            return;
        }

        // Add hook to logs on the GameManager
        gm.OnLog += HandleLog;
        // Add hook for unity logs
        Application.logMessageReceived += HandleUnityLog;

        // Build list of all commands
        allCommands = clientCommands.Concat(serverCommands).ToArray();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.Escape))
        {
            commandWindowObject.SetActive(!commandWindowObject.activeSelf);
            if (commandWindowObject.activeSelf)
            {
                Cursor.lockState = CursorLockMode.None;
                commandInput.ActivateInputField();
            } else
            {
                Cursor.lockState = CursorLockMode.Locked;
                
            }
        }
    }
}
