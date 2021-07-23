using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using tart;
using UnityEngine.SceneManagement;
using Photon.Realtime;
using ExitGames.Client.Photon;

// TODO unset self from static when being destroyed

// Delegate signature for alert events
public delegate void Alert(string message);

public class GameManager : MonoBehaviourPunCallbacks, IPunObservable, IOnEventCallback
{
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
        RoundStart,
        RoundOver,
        PlayerDied
    }

    // Set static for easy references.
    public static GameManager gm;

    // Game-level alerts
    public event Alert OnGameAlert;

    // Ref to the ui we need to enable after loading
    public GameObject ui;

    // The player prefab we spawn for ourselves
    public GameObject playerPrefab;

    // The round over noise.
    [SerializeField]
    AudioSource roundChange;
    [SerializeField]
    AudioClip preRound;
    [SerializeField]
    AudioClip roundStart;
    [SerializeField]
    AudioClip roundOver;

    // Current state of the game.
    // 0 - pre-round
    // 1 - active
    // 2 - post-round
    public GameState gameState = GameState.PreRound;
    private GameState previousGameState = GameState.PreRound;

    // Connected players. When a player script wakes, it find a GameManager 
    // and adds itself via AddPlayer.
    public List<Player> players;

    // Minimum numbers of players required to start a round.
    public int minPlayers = 2;

    // Role list
    private List<TartRole> roles;

    // Ref to the UI alert script
    private UI_alert alert;

    // Round delay times (seconds)
    public float preRoundTime = 15;
    public float curPreRoundTime;

    public float postRoundTime = 15;
    public float curPostRoundTime;

    // Player spawn locations
    public List<GameObject> spawnLocations;

    // Sync values
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(gameState);
            stream.SendNext(curPreRoundTime);
            stream.SendNext(curPostRoundTime);
        }
        else
        {
            this.gameState = (GameState)stream.ReceiveNext();
            this.curPreRoundTime = (float)stream.ReceiveNext();
            this.curPostRoundTime = (float)stream.ReceiveNext();
        }
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

        Debug.Log("[GameManager] Started. State is " + gameState);

        ui.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        // Check all players still exist
        // TODO not sure about this one!!
        players.ForEach(delegate (Player p)
        {
            if (!p) players.Remove(p);
        });

        // Check if the game state changed
        if (gameState != previousGameState)
        {
            GameStateChanged(previousGameState, gameState);
            previousGameState = gameState;
        }

        // Only the server should handle round control
        if (!PhotonNetwork.IsMasterClient) return;

        switch (gameState)
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
                    gameState = GameState.Active;
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
                    gameState = GameState.PostRound;
                    // Send out RoundOver event
                    // Content format is { IdOfWinningTeam }
                    object[] content = new object[] { 2 };
                    RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All }; // All sends to us as well
                    PhotonNetwork.RaiseEvent((byte)Events.RoundOver, content, raiseEventOptions, SendOptions.SendReliable);

                }
                // Innocent win.
                if (lTraitors == 0) {
                    Debug.Log("[GM] Innocents win!");
                    gameState = GameState.PostRound;
                    // Send out RoundOver event
                    // Content format is { IdOfWinningTeam }
                    object[] content = new object[] { 1 };
                    RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All }; // All sends to us as well
                    PhotonNetwork.RaiseEvent((byte)Events.RoundOver, content, raiseEventOptions, SendOptions.SendReliable);
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
                gameState = GameState.PreRound;
                break;

            default:
                break;
        }
    }

    void GameStateChanged(GameState oldState, GameState newState)
    {
        Alert("[GM] Game state changed from " + oldState + " to " + newState);

        if (newState == GameState.PreRound)
        {
            players.ForEach(p =>
            {
                if (!PhotonNetwork.IsMasterClient) return;
                p.photonView.RPC("RpcReset", RpcTarget.All);
            });

            roundChange.clip = preRound;
            roundChange.Play();
        }

        if (newState == GameState.Active)
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
                if (!PhotonNetwork.IsMasterClient) return;
                int roleId = roles[index] == 0 ? 1 : roles[index];
                p.photonView.RPC("SetRoleById", RpcTarget.All, roleId);
                index++;
            });

            roundChange.clip = roundStart;
            roundChange.Play();

            // Send out event if we're server
            // TODO have a simple wrapper method for events sent to everyone
            if (PhotonNetwork.IsMasterClient)
            {
                // Send out RoundStart event
                // Content format is {  }
                object[] content = new object[] { };
                RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All }; // All sends to us as well
                PhotonNetwork.RaiseEvent((byte)Events.RoundStart, content, raiseEventOptions, SendOptions.SendReliable);
            }
            Debug.Log("[GM] Round started!");
        }

        if (newState == GameState.PostRound)
        {
            roundChange.clip = roundOver;
            roundChange.Play();
        }
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
        return players.Find(p => p.id == id);
    }

    public void AddPlayer(Player player)
    {
        if (GetPlayerByID(player.id))
        {
            Alert("[GM] Tried to add player that already exists!");
            return;
        }
        players.Add(player);
    }

    // This is kinda a temp thing to make sure our player goes back to the main menu when disconnected
    public override void OnLeftRoom()
    {
        SceneManager.LoadScene(0);
    }

    public void Start()
    {
        // TEMP. Makes a player prefab for us. Doesn't check for rounds in progress
        if (playerPrefab == null)
        {
            Debug.LogError("<Color=Red><a>Missing</a></Color> playerPrefab Reference. Please set it up in GameObject 'Game Manager'", this);
        }
        else
        {
            Debug.LogFormat("We are Instantiating LocalPlayer from {0}", Application.loadedLevelName);
            // we're in a room. spawn a character for the local player. it gets synced by using PhotonNetwork.Instantiate
            PhotonNetwork.Instantiate(this.playerPrefab.name, spawnLocations[0].transform.position, Quaternion.identity, 0); // TODO need to choose a spawnpoint
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        int eventCode = photonEvent.Code;

        if (eventCode == (int)Events.RoundOver)
        {
            object[] data = (object[])photonEvent.CustomData;
            int winningTeam = (int)data[0];

            switch (winningTeam)
            {
                case 1:
                    Alert("Innocents win!");
                    break;

                case 2:
                    Alert("Traitors win!");
                    break;

                default:
                    Alert("Unknown team win!");
                    break;
            }
        }

        if (eventCode == (int)Events.RoundStart)
        {
            Alert("Round started!");
        }
        
    }

    // Called by a client when destroying an item (eg, picking it up from the ground)
    [PunRPC]
    public void DestroyItem(int photonViewID)
    {
        PhotonView pv = PhotonView.Find(photonViewID);
        PhotonNetwork.Destroy(pv);
    }
}
