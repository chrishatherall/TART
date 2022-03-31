using Photon.Pun;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static GameManager;
using static LogManager;

public class UI_commandWindow : MonoBehaviourPun
{
    readonly string logSrc = "UI_CW";

    string[] allCommands;     // Concat if client+server commands
    string[] clientCommands = // Commands which can be run by all players
    {
        "KILL"
    };
    string[] serverCommands = // Commands run only by the server/host
    {
        "SPAWN",
        "ROUNDRESTART",
        "BOT"
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

    [SerializeField]
    int maxLogLength = 1000; // in characters

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
            lm.Log(logSrc, $"Unknown command: {command}.");
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
        lm.Log(logSrc,$"{pmi.Sender.ActorNumber}:{pmi.Sender.NickName} entered [{commandInput.text}].");

        // Ensure all commands are in uppercase
        string[] split = command.ToUpper().Split(' ');

        // If the command is a server command, check it's from the master client
        if (serverCommands.Contains(split[0]) && !pmi.Sender.IsMasterClient)
        {
            lm.Log(logSrc, $"Ignoring command [{commandInput.text}]. Not server.");
            return;
        }

        // Get player who sent the command
        Player player = gm.GetPlayerById(pmi.Sender.ActorNumber);
        if (!player)
        {
            lm.LogError(logSrc, $"Couldnt find player {pmi.Sender.ActorNumber}.");
            return;
        }

        // Command-specific logic
        switch (split[0])
        {
            case "SPAWN":
                // Check the item we want to spawn is legit
                if (spawnableItems.Contains(split[1]))
                {
                    if (!player.character) return;
                    player.character.photonView.RPC("RpcDropItem", RpcTarget.MasterClient, split[1]);
                } else
                {
                    lm.Log(logSrc, $"Invalid SPAWN value: {split[1]}.");
                }
                break;

            case "BOT":
                if (!player.character) return;
                object[] instanceData = new object[1];
                instanceData[0] = "isBot"; // TODO maybe some pre-defined structure of character options? Would need serialiser
                PhotonNetwork.InstantiateSceneObject("character", player.character.lastHit.point, Quaternion.identity, 0 , instanceData);
                break;

            case "KILL":
                // Currently you can only kill your own player
                if (!player.character) return;
                player.character.photonView.RPC("InstaKill", RpcTarget.All, 999);
                break;

            case "ROUNDRESTART":
                // Set round to postround
                gm.CurrentGameState = GameState.PostRound;
                // Set roundovertime to something smaller than maxroundovertime to make the postround quick
                gm.curPostRoundTime = 1;
                // This should initiate the postround cleanup, give it time, then start the preround spawning
                // TODO somehow force a draw with no points
                break;

            default:
                lm.Log(logSrc, $"Invalid command: {split[0]}.");
                break;
        }
    }

    public void HandleLog(string message, bool isError)
    {
        string newText = logText.text + "\n" + message;
        // Trim text
        if (newText.Length > maxLogLength)
        {
            newText = newText.Substring(newText.Length-maxLogLength);
        }
        logText.text = newText;

        // TODO colour errors in red
        // TODO scroll down automatically
    }

    // Start is called before the first frame update
    void Start()
    {
        if (!commandWindowObject || !commandInput || !logText)
        {
            lm.LogError(logSrc, "Missing references");
            this.enabled = false;
            return;
        }

        // Populate our log with past logs
        logText.text = lm.GetFullLog();

        // Add hook to logs on the GameManager
        lm.OnLog += HandleLog;

        // Build list of all commands
        allCommands = clientCommands.Concat(serverCommands).ToArray();
    }

    void Update()
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
