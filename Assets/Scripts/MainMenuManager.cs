using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using System.Linq;

public class MainMenuManager : MonoBehaviourPunCallbacks
{
	public static string RandomString(int length)
	{
		System.Random r = new System.Random();
		const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
		return new string(Enumerable.Repeat(chars, length)
		  .Select(s => s[r.Next(s.Length)]).ToArray());
	}

	[Tooltip("The Ui Panel to let the user enter name, connect and play")]
	[SerializeField]
	private GameObject controlPanel;

	[Tooltip("The Ui element containing the player name")]
	[SerializeField]
	private Text playerNameTextbox;	
	
	[Tooltip("The Ui element containing the room code")]
	[SerializeField]
	private Text roomCodeTextbox;

    [Tooltip("The Ui Text to inform the user about the connection progress")]
    [SerializeField]
    private Text feedbackText;

    [Tooltip("The Ui Text to show the game version")]
    [SerializeField]
    private Text versionText;

    [Tooltip("The maximum number of players per room")]
	[SerializeField]
	private byte maxPlayersPerRoom = 4;

	[Tooltip("The game mode dropdown")]
	[SerializeField]
	private Dropdown gamemodeDD;

	[SerializeField]
	Button joinBtn;
	[SerializeField]
	Button hostBtn;

	/// <summary>
	/// Keep track of the current process. Since connection is asynchronous and is based on several callbacks from Photon, 
	/// we need to keep track of this to properly adjust the behavior when we receive call back by Photon.
	/// Typically this is used for the OnConnectedToMaster() callback.
	/// </summary>
	//bool isConnecting;

	/// <summary>
	/// This client's version number. Users are separated from each other by gameVersion (which allows you to make breaking changes).
	/// </summary>
	string gameVersion = "1";

	void Log(string msg)
    {
		feedbackText.text = msg + "\n" + feedbackText.text;
    }

	void Awake()
	{

		// #Critical
		// this makes sure we can use PhotonNetwork.LoadLevel() on the master client and all clients in the same room sync their level automatically
		PhotonNetwork.AutomaticallySyncScene = true;

		if (!PhotonNetwork.IsConnected)
        {
			PhotonNetwork.ConnectUsingSettings();
			PhotonNetwork.GameVersion = this.gameVersion;
		}

		versionText.text = Application.version;

		Cursor.lockState = CursorLockMode.None;
	}

	/// <summary>
	/// Start the connection process. 
	/// - If already connected, we attempt joining a random room
	/// - if not yet connected, Connect this application instance to Photon Cloud Network
	/// </summary>
	public void Host()
	{
		// Set player name
		PhotonNetwork.NickName = playerNameTextbox.text.Length > 0 ? playerNameTextbox.text : "Player";

		// hide the Play button for visual consistency
		controlPanel.SetActive(false);

		// we check if we are connected or not, we join if we are , else we initiate the connection to the server.
		if (PhotonNetwork.IsConnected)
		{
			Log("Creating room for " + gamemodeDD.captionText.text + "...");
			// #Critical we need at this point to attempt joining a Random Room. If it fails, we'll get notified in OnJoinRandomFailed() and we'll create one.
			RoomOptions ro = new RoomOptions { MaxPlayers = this.maxPlayersPerRoom };
			ro.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable();
			ro.CustomRoomProperties.Add("gamemode", gamemodeDD.captionText.text);
			PhotonNetwork.CreateRoom(RandomString(4), ro) ; 
			// TODO use custom room settings to set game type. This can be read by GM
		}
		else
		{
			Log("Connecting...");
			// #Critical, we must first and foremost connect to Photon Online Server.
			PhotonNetwork.ConnectUsingSettings();
			PhotonNetwork.GameVersion = this.gameVersion;
			controlPanel.SetActive(true);
		}
	}

    public void Join()
    {
		// Set player name
		PhotonNetwork.NickName = playerNameTextbox.text.Length > 0 ? playerNameTextbox.text : "Player";

		// hide the Play button for visual consistency
		controlPanel.SetActive(false);

		// Set room code to uppercase
		roomCodeTextbox.text = roomCodeTextbox.text.ToUpper();

		if (PhotonNetwork.IsConnected)
		{
			Log("Joining Room...");
			// #Critical we need at this point to attempt joining a Random Room. If it fails, we'll get notified in OnJoinRandomFailed() and we'll create one.
			PhotonNetwork.JoinRoom(roomCodeTextbox.text);
		}
		else
		{
			Log("Connecting...");
			// #Critical, we must first and foremost connect to Photon Online Server.
			PhotonNetwork.ConnectUsingSettings();
			PhotonNetwork.GameVersion = this.gameVersion;
			controlPanel.SetActive(true);
		}
	}

    /// <summary>
    /// Called after the connection to the master is established and authenticated
    /// </summary>
    public override void OnConnectedToMaster()
    {
		Log("Connected to primary server");
		hostBtn.interactable = true;
		joinBtn.interactable = true;
    }


    /// <summary>
    /// Called when a JoinRandom() call failed. The parameter provides ErrorCode and message.
    /// </summary>
    /// <remarks>
    /// Most likely all rooms are full or no rooms are available. <br/>
    /// </remarks>

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Log("Room join failed. " + message);
		controlPanel.SetActive(true);
	} 
	public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Log("Create room failed. " + message);
		controlPanel.SetActive(true);
	}

    /// <summary>
    /// Called after disconnecting from the Photon server.
    /// </summary>
    public override void OnDisconnected(DisconnectCause cause)
	{
		Log("Disconnected. " + cause.ToString());
		hostBtn.interactable = false;
		joinBtn.interactable = false;
		// #Critical: we failed to connect or got disconnected. There is not much we can do. Typically, a UI system should be in place to let the user attemp to connect again.

		controlPanel.SetActive(true);
	}



	/// <summary>
	/// Called when entering a room (by creating or joining it). Called on all clients (including the Master Client).
	/// </summary>
	/// <remarks>
	/// This method is commonly used to instantiate player characters.
	/// If a match has to be started "actively", you can call an [PunRPC](@ref PhotonView.RPC) triggered by a user's button-press or a timer.
	///
	/// When this is called, you can usually already access the existing players in the room via PhotonNetwork.PlayerList.
	/// Also, all custom properties should be already available as Room.customProperties. Check Room..PlayerCount to find out if
	/// enough players are in the room to start playing.
	/// </remarks>
	public override void OnJoinedRoom()
	{
		Log("Joined room.");

		// #Critical: We only load if we are the first player, else we rely on  PhotonNetwork.AutomaticallySyncScene to sync our instance scene.
		if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
		{
			Log("Loading level as first player..");

			// #Critical
			// Load the Room Level. 
			PhotonNetwork.LoadLevel("glockmansion2");

		}
	}
}
