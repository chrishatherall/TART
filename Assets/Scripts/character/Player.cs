using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;
using tart;
using static GameManager;
using static LogManager;

public class Player : MonoBehaviourPunCallbacks, IPunObservable
{
    readonly string logSrc = "Player";

    // Role
    private TartRole _role;

    #region Sync'd variables
    // Oil is effectively hitpoints
    public int oil = 100;
    public int maxOil = 100;
    // is the player dead
    public bool isDead = false;
    // Local player is loaded and ready to play a round
    public bool isReady = false;
    // Name of this player
    public string nickname;
    // The unique actor number provided by photon to networked players (alias is ID)
    public int actorNumber;
    // The thing this player is looking at
    public Vector3 aim;
    #endregion

    // Flag for bots
    public bool isBot;

    // Our body parts
    BodyPart[] bodyParts;

    // Our held item, and script
    public GameObject heldItem;
    public HeldItem heldItemScript;
    // The item anchor gameobject
    public GameObject itemAnchor;


    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // TODO sync bodypart damage so we can create damage visuals on specific body parts

        if (stream.IsWriting)
        {
            // We own this player: send the others our data
            stream.SendNext(oil);
            stream.SendNext(maxOil);
            stream.SendNext(isDead);
            stream.SendNext(isReady);
            stream.SendNext(nickname);
            stream.SendNext(actorNumber);
            stream.SendNext(aim);
        }
        else
        {
            // Network player, receive data
            this.oil = (int)stream.ReceiveNext();
            this.maxOil = (int)stream.ReceiveNext();
            this.isDead = (bool)stream.ReceiveNext();
            this.isReady = (bool)stream.ReceiveNext();
            this.nickname = (string)stream.ReceiveNext();
            this.actorNumber = (int)stream.ReceiveNext();
            this.aim = (Vector3)stream.ReceiveNext();
        }
    }

    // Public access for the role
    public TartRole Role { get => _role; }

    // ID is a alias for actorNumber
    public int ID { get => actorNumber; }

    void Start()
    {
        // GM might not be ready yet.
        if (!gm)
        {
            lm.LogError(logSrc,"GM not ready!");
            return;
        }

        // Set ourselves as default role
        this._role = gm.GetRoleFromID(0);

        // Find our body parts
        bodyParts = GetComponentsInChildren<BodyPart>();

        // Set as ready, and apply nickname when the local player has loaded
        if (photonView.IsMine)
        {
            if (isBot)
            {
                actorNumber = 10000 + this.photonView.ViewID; // Probably not the safest
                nickname = "BOT" + this.photonView.ViewID;
            }
            else
            {
                actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
                nickname = PhotonNetwork.LocalPlayer.NickName;
                SetLayer(7); // Set to localplayer layer
            }
            isReady = true;
        }

        // Announce self to GM.
        lm.Log(logSrc, "Started. Announcing to GameManager.");
        gm.AddPlayer(this);
    }

    [PunRPC]
    public void SetRoleById(int id)
    {
        _role = gm.GetRoleFromID(id);
        // Create an alert for the local player
        if (photonView.IsMine && !isBot) gm.Alert($"Role is now {_role.Name}!");
    }

    [PunRPC]
    public void Reset()
    {
        oil = 100;
        maxOil = 100;
        // Set damage of each BodyPart to 0
        foreach(BodyPart bp in bodyParts) { bp.Damage = 0; }
        isDead = false;
        this._role = gm.GetRoleFromID(0);
        // Find an Animator and turn it on
        Animator ani = GetComponent<Animator>();
        if (ani) ani.enabled = true;
    }

    [PunRPC]
    public void TakeDamage(int dmg, string bodyPartName)
    {
        // Don't deal with damage if we don't own this player
        if (!photonView.IsMine) return;

        // Can't take more damage if we're dead
        if (isDead) return;

        // Find the BodyPart that took damage
        BodyPart bodyPart = GetBodyPartByName(bodyPartName);
        if (bodyPart)
        {
            bodyPart.Damage += dmg;
        } else
        {
            lm.LogError(logSrc, $"Could not find body part '{bodyPartName}'");
        }

        // To make damage more responsive, kill player instantly if damage > oil
        if (GetDamage() > oil)
        {
            oil = 0;
            Die("Unknown", "Oil loss");
            if (!isBot) gm.Alert("DEAD");
        }
    }

    // Ticks happen once a second. Why though? Is it so we reduce network sync traffic?
    // It would be cleaner to ignore this and round values for the UI.
    private float msSinceLastTick = 0;
    public void Update()
    {
        if (!photonView || !photonView.IsMine) return;
        // TODO this should only run on one 
        msSinceLastTick += Time.deltaTime;
        if (msSinceLastTick > 1) // One second
        {
            msSinceLastTick = 0;

            int dmg = GetDamage();

            // Leak oil if damaged
            if (dmg > 0 && oil > 0)
            {
                oil -= dmg; // TODO: Hook into this change to create oil leaks on the floor. Oil can still leak if dead via other means, creating cool oil pools.
                // If we're out of oil and not dead, die.
                if (oil <= 0 && !isDead)
                {
                    oil = 0;
                    Die();
                }
            }
        }
    }

    BodyPart GetBodyPartByName(string name)
    {
        return bodyParts.First(bp => bp.gameObject.name == name);
    }

    public int GetDamage()
    {
        return bodyParts.Sum(bp => bp.Damage);
    }

    // Called when something causes our death
    public void Die(string source = "Unknown", string method = "Unknown")
    {
        lm.Log(logSrc, $"{nickname} died.");

        isDead = true;

        // Find an Animator and turn it off
        Animator ani = GetComponent<Animator>();
        if (ani) ani.enabled = false;
        // TODO at this point we should turn all the ragdoll things (joints) ON, for efficiency?

        // We don't need to do anything else if this isn't our player
        if (!this.photonView.IsMine) return;

        // TODO this might be better hooking into an event to increase modularity
        // Try to drop our held item
        FpsController fpsc = GetComponent<FpsController>();
        if (fpsc) fpsc.TryDropHeldItem();

        // Dont do any UI stuff for bots
        if (!isBot)
        {
            // Turn on the dead screen
            gm.DeadScreen.SetActive(true);
            // Set dead screen message
            gm.DeathDetailsText.text = $"Killed by {source} with {method}";
            // Activate mouse
            Cursor.lockState = CursorLockMode.None;
        }
    }

    void SetLayer(int layer)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(includeInactive: true))
        {
            t.gameObject.layer = layer;
        }
    }
}
