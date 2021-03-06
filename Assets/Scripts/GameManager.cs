using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TART;
using UnityEngine.SceneManagement;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Linq;
using UnityEngine.UI;
using static LogManager;

// Delegate signature for alert events
public delegate void Alert(string message);

public class GameManager : MonoBehaviourPunCallbacks, IPunObservable, IOnEventCallback
{
    readonly string logSrc = "GM";
    // Enum for the different game states
    public enum GameState
    {
        PreRound,
        Active,
        PostRound
    }

    // Set static for easy reference in other scripts.
    public static GameManager gm;

    // Game-level alerts
    public event Alert OnGameAlert;

    // Ref to the ui we need to enable after loading
    public GameObject ui;
    // The player prefab we spawn for ourselves
    public GameObject playerPrefab;
    // The character prefab for player characters
    public GameObject characterPrefab;

    // Ref to the special weapon UI elements for Deathmatch
    public UnityEngine.UI.Image curGrenadePointsImage;
    public UnityEngine.UI.Image maxGrenadePointsImage;
    public UnityEngine.UI.Image curC4PointsImage;
    public UnityEngine.UI.Image maxC4PointsImage;
    public UnityEngine.UI.Text killCountText;

    public int DeathmatchRequiredKills = 25;

    // Round-change noises.
    [SerializeField]
    AudioSource roundChange;
    [SerializeField]
    AudioClip preRound;
    [SerializeField]
    AudioClip roundStart;
    [SerializeField]
    AudioClip roundOver;

    [SerializeField]
    public AudioClip cannotPlaceClip;

    // Current and previous state of the game.
    private GameState _curGameState = GameState.PreRound;
    private GameState previousGameState = GameState.PreRound;

    // Gamemode
    public string gamemode;

    // Active characters. Character scripts add themselves on startup via AddPlayer
    public List<Character> characters;

    // Connected players. Player scripts add themselves on startup.
    public List<Player> players;

    // Reference to the local player
    public Player localPlayer;

    // Minimum numbers of players required to start a round.
    public int minPlayers = 2;

    // Role list
    private List<TartRole> roles;

    // Round delay times (seconds)
    float DC_preRoundTime = 15;
    float DC_postRoundTime = 15;
    float DM_preRoundTime = 1;
    float DM_postRoundTime = 5;
    public float curPreRoundTime;
    public float curPostRoundTime;

    public float preRoundTime
    {
        get {
            switch (gamemode) {
                case "Deception": return DC_preRoundTime;
                case "Deathmatch": return DM_preRoundTime;
                default: return 1;
            }
        }
    }
    public float postRoundTime
    {
        get {
            switch (gamemode) {
                case "Deception": return DC_postRoundTime;
                case "Deathmatch": return DM_postRoundTime;
                default: return 1;
            }
        }
    }

    // Player spawn locations, discovered on startup
    public PlayerSpawn[] playerSpawnLocations;

    // List of gameobjects with spawnitems, needs to be set manually
    public List<GameObject> spawnTables;

    // The object which scene-spawned items/players should parent to, purely for cleanliness. 
    public Transform itemSpawnParent;
    public Transform playerSpawnParent;
    public Transform characterSpawnParent;

    // TODO these should be on some kind of player manager, which also hosts anything related to a specific player (death screen, spectator mode prefab, player ui, etc)
    // Basically this GM should only care about the game itself

    // The screen that shows when we die, giving us a respawn button in Deathmatch mode.
    public GameObject DeadScreen;
    public Text DeathDetailsText;

    // Footsteps
    FootstepPool[] footstepPools;

    public GameState CurrentGameState { 
        get => _curGameState; 
        set {
            previousGameState = _curGameState;
            _curGameState = value;
            if (previousGameState != _curGameState) GameStateChanged(previousGameState, _curGameState);
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

    // Close the game immediately
    public void Quit()
    {
        // Note: ignored in the editor
        Application.Quit();
    }

    // Disconnects from the server
    public void Disconnect()
    {
        PhotonNetwork.LeaveRoom();
        Application.LoadLevel(0);
    }

    void Awake()
    {
        // Add reference to ourself to the static GameManager
        gm = this;

        // Pull the gamemode from the Photon room settings
        gamemode = PhotonNetwork.CurrentRoom.CustomProperties["gamemode"].ToString();

        // Add roles
        roles = new List<TartRole>();
        switch (gamemode)
        {
            case "Deathmatch":
                roles.Add(new TartRole(0, "Murderer", Color.grey));
                break;

            case "Deception":
                roles.Add(new TartRole(0, "Spectator", Color.grey));
                roles.Add(new TartRole(1, "Innocent", Color.green));
                roles.Add(new TartRole(2, "Traitor", Color.red));
                break;
        }


        // Set our initial round timers
        curPreRoundTime = preRoundTime;
        curPostRoundTime = postRoundTime;

        // Find all player spawn points
        playerSpawnLocations = FindObjectsOfType<PlayerSpawn>();

        // Find all footstep pools
        footstepPools = FindObjectsOfType<FootstepPool>();

        lm.Log(logSrc,"Started. State is " + CurrentGameState);

        ui.SetActive(true);

        PhotonNetwork.AddCallbackTarget(this);
    }

    public void Start()
    {
        PhotonNetwork.SerializationRate = 100;

        // TEMP. Makes a player prefab for us. Doesn't check for rounds in progress
        if (playerPrefab == null)
        {
            lm.LogError(logSrc, "Missing playerPrefab Reference.");
            return;
        }

        Vector3 spawnPosition = new Vector3(0f, 1000f, 0f);
        // Spawn a Player for the local player
        GameObject player = PhotonNetwork.Instantiate(this.playerPrefab.name, spawnPosition, Quaternion.identity, 0);
        localPlayer = player.GetComponent<Player>();

        // Tell all scene spawn points to spawn their items
        SpawnSceneItems();
    }

    // Update is called once per frame
    void Update()
    {
        // Photon docs say do this maybe but it doesnt seem effective
        // PhotonNetwork.NetworkingClient.Service();

        // Remove any characters from the list that don't exist
        characters.RemoveAll(delegate (Character c) { return !c; });

        CalculateGameState();
    }

    // Run once per frame via Update. Changes game state if conditions are met.
    void CalculateGameState()
    {
        // Should be run by the server only
        if (!PhotonNetwork.IsMasterClient) return;

        switch (gamemode)
        {
            #region Deception
            case "Deception":
                // Determine state changes
                switch (_curGameState)
                {
                    // Pre-round
                    case GameState.PreRound:
                        // Check we have enough characters to start a round.
                        List<Character> readyCharacters = characters.FindAll(p => p.isReady);
                        if (readyCharacters.Count >= minPlayers)
                        {
                            // Use the pre-round time as a delay to avoid jarring gameplay
                            if (curPreRoundTime > 0)
                            {
                                curPreRoundTime -= Time.deltaTime;
                                break;
                            }
                            // Reset pre-round time
                            curPreRoundTime = DC_preRoundTime;
                            CurrentGameState = GameState.Active;
                        }
                        break;

                    // Round active
                    case GameState.Active:
                        // Check for an end-game state.
                        // Get living traitors and innocents.
                        int lTraitors = 0;
                        int lInnocents = 0;
                        characters.ForEach(delegate (Character p)
                        {
                            if (p.IsDead) return; // We don't care about dead people.
                            if (p.Role.ID == 1) lInnocents++;
                            if (p.Role.ID == 2) lTraitors++;
                        });
                        // Traitor win.
                        if (lInnocents == 0)
                        {
                            lm.Log(logSrc, "Traitors win!");
                            CurrentGameState = GameState.PostRound;
                            RaiseEvent(Events.TraitorWin);

                        }
                        // Innocent win.
                        if (lTraitors == 0)
                        {
                            lm.Log(logSrc, "Innocents win!");
                            CurrentGameState = GameState.PostRound;
                            RaiseEvent(Events.InnocentWin);
                        }
                        break;

                    // Post-round
                    case GameState.PostRound:
                        // Use the post-round time as a delay to avoid jarring gameplay
                        if (curPostRoundTime > 0)
                        {
                            curPostRoundTime -= Time.deltaTime;
                            break;
                        }
                        // Reset pre-round time
                        curPostRoundTime = DC_postRoundTime;
                        CurrentGameState = GameState.PreRound;
                        break;

                    default:
                        break;
                }
                break;
            #endregion

            #region Deathmatch
            case "Deathmatch":
                // Determine state changes
                switch (_curGameState)
                {
                    // Pre-round
                    case GameState.PreRound:
                        // Use the pre-round time as a delay to avoid jarring gameplay
                        if (curPreRoundTime > 0)
                        {
                            curPreRoundTime -= Time.deltaTime;
                            break;
                        }
                        // Reset pre-round time
                        curPreRoundTime = DM_preRoundTime;
                        CurrentGameState = GameState.Active;
                        break;

                    // Round active
                    case GameState.Active:
                        // Check for an end-game state.
                        // First to X kills, find a player with required number of kills
                        foreach (Player player in players)
                        {
                            if (player.DMPlayer && player.DMPlayer.kills >= DeathmatchRequiredKills)
                            {
                                // Send out event for the win
                                object[] eventData = new object[] { player.character.ID };
                                RaiseEvent(Events.DeathmatchWin, eventData);
                                Alert($"Winner: {player.character.nickname}");
                                ResetCharacters();

                                CurrentGameState = GameState.PostRound;
                            }
                        }

                        break;

                    // Post-round
                    case GameState.PostRound:
                        // Use the post-round time as a delay to avoid jarring gameplay
                        if (curPostRoundTime > 0)
                        {
                            curPostRoundTime -= Time.deltaTime;
                            break;
                        }
                        // Reset pre-round time
                        curPostRoundTime = DM_postRoundTime;
                        CurrentGameState = GameState.PreRound;
                        break;

                    default:
                        break;
                }
                break;
            #endregion

            default:
                lm.LogError(logSrc, "UNKNOWN GAMEMODE!");
                this.enabled = false;
                break;
        }

    }

    // Called when the game state changes
    void GameStateChanged(GameState oldState, GameState newState)
    {
        #region PreRound
        if (newState == GameState.PreRound)
        {
            lm.Log(logSrc,"Preround starting!");

            roundChange.clip = preRound;
            roundChange.Play();

            RaiseEvent(Events.Preround);

            // Tell item spawners to spawn their items
            SpawnSceneItems();

            ResetCharacters();            
        }
        #endregion

        #region Round start
        if (newState == GameState.Active)
        {
            lm.Log(logSrc,"Round started!");

            ResetCharacters();

            // The stuff below is Deception only
            //if (PhotonNetwork.IsMasterClient)
            //{
            //    List<Player> readyPlayers = players.FindAll(p => p.isReady);
            //    // Assign roles to players.
            //    // For a simple solution just take a random person to be the traitor
            //    int[] roles = new int[players.Count];
            //    System.Random rnd = new System.Random();
            //    roles[rnd.Next(0, players.Count - 1)] = 2; // Set random person as traitor
            //    int index = 0;
            //    readyPlayers.ForEach(delegate (Player p)
            //    {
            //        int roleId = roles[index] == 0 ? 1 : roles[index];
            //        p.photonView.RPC("SetRoleById", RpcTarget.All, roleId);
            //        index++;
            //    });
            //}

            roundChange.clip = roundStart;
            roundChange.Play();

            RaiseEvent(Events.RoundStart);
        }
        #endregion

        #region Postround
        if (newState == GameState.PostRound)
        {
            lm.Log(logSrc,"Round over!");
            roundChange.clip = roundOver;
            roundChange.Play();

            // Clear the scene of any spawned items
            ClearScene();
        }
        #endregion
    }

    public Transform GetCharacterSpawnLocation()
    {
        // TODO this should choose a random player spawn, but take into account other player's distance from
        // spawns. We don't want people spawning inside each other.
        if (playerSpawnLocations.Length == 0)
        {
            lm.LogError(logSrc, "Couldnt get player spawn, list is empty.");
            return this.transform;
        }
        int index = Random.Range(0, playerSpawnLocations.Length);
        return playerSpawnLocations[index].transform;
    }

    public void Alert(string message)
    {
        OnGameAlert.Invoke(message);
    }

    public TartRole GetRoleFromID(int id)
    {
        return roles.Find(r => r.ID == id);
    }

    public Character GetCharacterById(int id)
    {
        return characters.Find(p => p.ID == id);
    }

    public Player GetPlayerById(int id)
    {
        return players.Find(p => p.ID == id);
    }

    public Player GetPlayerByCharacterId (int id)
    {
        Character c = GetCharacterById(id);
        return c ? c.controllingPlayer : null;
    }

    public void AddCharacter(Character character)
    {
        if (GetCharacterById(character.ID))
        {
            lm.LogError(logSrc,"Tried to add character that already exists!");
            return;
        }
        characters.Add(character);
    }

    public void AddPlayer(Player player)
    {
        if (GetPlayerById(player.ID))
        {
            lm.LogError(logSrc, "Tried to add player that already exists!");
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
        //int eventCode = photonEvent.Code;
        //lm.Log(logSrc,$"Received Photon event code " + eventCode);
    }

    // Called by a client when destroying an item (eg, picking it up from the ground)
    [PunRPC]
    public void DestroyItem(int photonViewID)
    {
        PhotonView pv = PhotonView.Find(photonViewID);
        PhotonNetwork.Destroy(pv);
    }

    // Helper function for the Deathscreen UI to respawn our character. Remove in the future when we have a good ui
    public void RespawnMyCharacter()
    {
        if (localPlayer) localPlayer.Respawn();
    }

    public void ResetCharacters()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        foreach (Character c in characters)
        {
            // Reset players but don't force a full respawn
            c.photonView.RPC("Reset", RpcTarget.All, false);
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
            lm.Log(logSrc,$"Cannot find spawn table: '{listName}'.");
            return null;
        }
        // Get all spawnable items under this spawn table.
        SpawnListItem[] spawnItems = spawnTable.GetComponents<SpawnListItem>();
        // Check that the list isn't empty.
        if (spawnItems.Length == 0)
        {
            lm.Log(logSrc,$"Spawn table '{listName}' is empty.");
            return null;
        }
        // Return a random item from the list. TODO this ignores weighting
        return spawnItems[Random.Range(0, spawnItems.Length)].worldPrefab;
    }

    public AudioClip GetFootstepByMaterial(PhysicMaterial material)
    {
        // Physics materials will be instanced, and " (Instance)" is added to the name, which we need to remove.
        string matName = material.name.Replace(" (Instance)", "");
        // Find a pool that matches our provided physics material
        FootstepPool fsp = footstepPools.FirstOrDefault(fsp => fsp.material.name == matName);
        // If we didn't find one to match the physics material, use the default
        if (!fsp) fsp = footstepPools.FirstOrDefault(fsp => fsp.defaultPool);
        // If we didn't find a default, things have gone wrong.
        if (!fsp)
        {
            lm.LogError(logSrc, "Could not find a default footstep pool!");
            return null;
        }
        // Return a random footstep from the pool
        return fsp.GetRandomFootstep();
    }
}
