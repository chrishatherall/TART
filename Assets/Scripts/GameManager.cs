﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using tart;
using UnityEngine.SceneManagement;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Linq;

// Delegate signature for alert events
public delegate void Alert(string message);
// Delegate for log messages with error flag
public delegate void LogEntry(string message, bool error);

public class GameManager : MonoBehaviourPunCallbacks, IPunObservable, IOnEventCallback
{
    // Enum for the different game states
    public enum GameState
    {
        PreRound,
        Active,
        PostRound
    }
    // Events for STUFF happening. This should contain loads of things, so we can easily make weird weapons and items.
    public enum Events
    {
        AutoSomething,
        Preround,     // Preround started
        RoundStart,   // A round began
        Postround,    // A round ended, postround began
        InnocentWin,
        TraitorWin,
        PlayerDied    // A player died during a round
    }

    // Set static for easy reference in other scripts.
    public static GameManager gm;

    // Game-level alerts
    public event Alert OnGameAlert;
    // Log events
    public event LogEntry OnLog;

    // Ref to the ui we need to enable after loading
    public GameObject ui;
    // The player prefab we spawn for ourselves
    public GameObject playerPrefab;

    // Round-change noises.
    [SerializeField]
    AudioSource roundChange;
    [SerializeField]
    AudioClip preRound;
    [SerializeField]
    AudioClip roundStart;
    [SerializeField]
    AudioClip roundOver;

    // Current and previous state of the game.
    private GameState _curGameState = GameState.PreRound;
    private GameState previousGameState = GameState.PreRound;

    // Connected players. Player scripts add themselves on startup via AddPlayer
    public List<Player> players;

    // Minimum numbers of players required to start a round.
    public int minPlayers = 2;

    // Role list
    private List<TartRole> roles;

    // Round delay times (seconds)
    public float preRoundTime = 15;
    public float curPreRoundTime;
    public float postRoundTime = 15;
    public float curPostRoundTime;

    // Player spawn locations, discovered on startup
    public PlayerSpawn[] playerSpawnLocations;

    // List of gameobjects with spawnitems, needs to be set manually
    public List<GameObject> spawnTables;

    public GameState CurrentGameState { 
        get => _curGameState; 
        set {
            previousGameState = _curGameState;
            _curGameState = value;
            GameStateChanged(previousGameState, _curGameState);
        }
    }

    // Sync values
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(CurrentGameState);
            stream.SendNext(curPreRoundTime);
            stream.SendNext(curPostRoundTime);
        }
        else
        {
            this.CurrentGameState = (GameState)stream.ReceiveNext();
            this.curPreRoundTime = (float)stream.ReceiveNext();
            this.curPostRoundTime = (float)stream.ReceiveNext();
        }
    }

    public void LogError(string msg)
    {
        OnLog.Invoke(msg, true);
    }    
    public void Log(string msg)
    {
        OnLog.Invoke(msg, false);
    }

    void Awake()
    {
        // Add reference to ourself to the static GameManager
        gm = this;
        // Add roles
        roles = new List<TartRole>();
        roles.Add(new TartRole(0, "Spectator", Color.grey));
        roles.Add(new TartRole(1, "Innocent", Color.green));
        roles.Add(new TartRole(2, "Traitor", Color.red));

        // Set our initial round timers
        curPreRoundTime = preRoundTime;
        curPostRoundTime = postRoundTime;

        // Find all player spawn points
        playerSpawnLocations = FindObjectsOfType<PlayerSpawn>();

        // This is an empty function so onlog can invoke SOMETHING. It errors otherwise.
        OnLog += (m,b) => { };

        Log("[GM] Started. State is " + CurrentGameState);

        ui.SetActive(true);
    }

    public void Start()
    {
        // TEMP. Makes a player prefab for us. Doesn't check for rounds in progress
        if (playerPrefab == null)
        {
            LogError("[GM] Missing playerPrefab Reference.");
        }
        else
        {
            // Spawn a character for the local player
            PhotonNetwork.Instantiate(this.playerPrefab.name, GetPlayerSpawnLocation(), Quaternion.identity, 0);
        }

        // Tell all scene spawn points to spawn their items
        SpawnSceneItems();
    }

    // Update is called once per frame
    void Update()
    {
        // Remove any players from the list that don't exist
        players.RemoveAll(delegate (Player p) { return !p; });

        // Further code is run by the server only
        if (!PhotonNetwork.IsMasterClient) return;

        // Determine state changes
        switch (_curGameState)
        {
            // Pre-round
            case GameState.PreRound:
                // Check we have enough players to start a round.
                List<Player> readyPlayers = players.FindAll(p => p.isReady);
                if (readyPlayers.Count >= minPlayers) {
                    // Use the pre-round time as a delay to avoid jarring gameplay
                    if (curPreRoundTime > 0)
                    {
                        curPreRoundTime -= Time.deltaTime;
                        break;
                    }
                    // Reset pre-round time
                    curPreRoundTime = preRoundTime;
                    CurrentGameState = GameState.Active;
                }
                break;

            // Round active
            case GameState.Active:
                // Check for an end-game state.
                // Get living traitors and innocents.
                int lTraitors = 0;
                int lInnocents = 0;
                players.ForEach(delegate (Player p)
                {
                    if (p.isDead) return; // We don't care about dead people.
                    if (p.Role.ID == 1) lInnocents++;
                    if (p.Role.ID == 2) lTraitors++;
                });
                // Traitor win.
                if (lInnocents == 0)
                {
                    Debug.Log("[GM] Traitors win!");
                    CurrentGameState = GameState.PostRound;
                    RaiseEvent(Events.TraitorWin);

                }
                // Innocent win.
                if (lTraitors == 0) {
                    Debug.Log("[GM] Innocents win!");
                    CurrentGameState = GameState.PostRound;
                    RaiseEvent(Events.InnocentWin);
                }
                break;

            // Post-round
            case GameState.PostRound:
                // Use the pre-round time as a delay to avoid jarring gameplay
                if (curPostRoundTime > 0)
                {
                    curPostRoundTime -= Time.deltaTime;
                    break;
                }
                // Reset pre-round time
                curPostRoundTime = postRoundTime;
                CurrentGameState = GameState.PreRound;
                break;

            default:
                break;
        }
    }

    // Called when the game state changes
    void GameStateChanged(GameState oldState, GameState newState)
    {
        Alert("[GM] Game state changed from " + oldState + " to " + newState);

        #region PreRound
        if (newState == GameState.PreRound)
        {
            Log("[GM] Preround starting!");

            roundChange.clip = preRound;
            roundChange.Play();

            RaiseEvent(Events.Preround);

            // Tell item spawners to spawn their items
            SpawnSceneItems();

            ResetPlayers();            
        }
        #endregion

        #region Round start
        if (newState == GameState.Active)
        {
            Log("[GM] Round started!");

            if (PhotonNetwork.IsMasterClient)
            {
                List<Player> readyPlayers = players.FindAll(p => p.isReady);
                // Assign roles to players.
                // For a simple solution just take a random person to be the traitor
                int[] roles = new int[players.Count];
                System.Random rnd = new System.Random();
                roles[rnd.Next(0, players.Count - 1)] = 2; // Set random person as traitor
                int index = 0;
                readyPlayers.ForEach(delegate (Player p)
                {
                    int roleId = roles[index] == 0 ? 1 : roles[index];
                    p.photonView.RPC("SetRoleById", RpcTarget.All, roleId);
                    index++;
                });
            }

            roundChange.clip = roundStart;
            roundChange.Play();

            RaiseEvent(Events.RoundStart);
        }
        #endregion

        #region Postround
        if (newState == GameState.PostRound)
        {
            Log("[GM] Round over!");
            roundChange.clip = roundOver;
            roundChange.Play();

            // Clear the scene of any spawned items
            ClearScene();
        }
        #endregion
    }

    public Vector3 GetPlayerSpawnLocation()
    {
        // TODO this should choose a random player spawn, but take into account other player's distance from
        // spawns. We don't want people spawning inside each other.
        if (playerSpawnLocations.Length == 0)
        {
            LogError("[GM] Couldnt get player spawn, list is empty.");
            return Vector3.zero;
        }
        int index = Random.Range(0, playerSpawnLocations.Length);
        return playerSpawnLocations[index].transform.position;
    }

    public void Alert(string message)
    {
        OnGameAlert.Invoke(message);
    }

    public TartRole GetRoleFromID(int id)
    {
        return roles.Find(r => r.ID == id);
    }

    public Player GetPlayerByID(int id)
    {
        return players.Find(p => p.ID == id);
    }

    public Player GetPlayerByActorNumber(int id)
    {
        return players.Find(p => p.actorNumber == id);
    }

    public void AddPlayer(Player player)
    {
        if (GetPlayerByID(player.ID))
        {
            LogError("[GM] Tried to add player that already exists!");
            return;
        }
        players.Add(player);
    }

    // This is kinda a temp thing to make sure our player goes back to the main menu when disconnected
    public override void OnLeftRoom()
    {
        SceneManager.LoadScene(0);
    }

    public void RaiseEvent(Events eventCode, object[] content)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All }; // All sends to us as well
            PhotonNetwork.RaiseEvent((byte)eventCode, content, raiseEventOptions, SendOptions.SendReliable);
        }
    }
    public void RaiseEvent(Events eventCode)
    {
        RaiseEvent(eventCode, new object[] { });
    }

    public void OnEvent(EventData photonEvent)
    {
        int eventCode = photonEvent.Code;
        Log($"[GM] Received Photon event code " + eventCode);
    }

    // Called by a client when destroying an item (eg, picking it up from the ground)
    [PunRPC]
    public void DestroyItem(int photonViewID)
    {
        PhotonView pv = PhotonView.Find(photonViewID);
        PhotonNetwork.Destroy(pv);
    }

    public void ResetPlayers()
    {
        // TODO maybe respawn players entirely

        if (!PhotonNetwork.IsMasterClient) return;
        foreach (Player p in players)
        {
            p.photonView.RPC("Reset", RpcTarget.All);
        }
    }

    public void ClearScene()
    {
        // Destroy anything with a photonview, including players and spawned objects
        // DO NOT destroy doors and such 
        // Players + Pickups? Might be best to mark objects in the future?
    }

    public void SpawnSceneItems()
    {
        ItemSpawn[] itemSpawns = FindObjectsOfType<ItemSpawn>();
        foreach (ItemSpawn spawn in itemSpawns)
        {
            spawn.SpawnItem();
        }
    }

    public GameObject GetItemFromSpawnList(string listName)
    {
        // Find the right spawn list
        GameObject spawnTable = spawnTables.Find(st => st.name == listName);
        if (!spawnTable)
        {
            Log($"[GM] Cannot find spawn table: '{listName}'.");
            return null;
        }
        // Get all spawnable items under this spawn table.
        SpawnListItem[] spawnItems = spawnTable.GetComponents<SpawnListItem>();
        // Check that the list isn't empty.
        if (spawnItems.Length == 0)
        {
            Log($"[GM] Spawn table '{listName}' is empty.");
            return null;
        }
        // Return a random item from the list. TODO this ignores weighting
        return spawnItems[Random.Range(0, spawnItems.Length)].worldPrefab;
    }
}
