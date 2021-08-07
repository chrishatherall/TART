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
    private bool _isDead = false;
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

    // Movement
    bool _isCrouching;
    public GameObject topOfHead;
    Vector3 standingHeadPos;

    // Our body parts
    BodyPart[] bodyParts;

    // Our held item, and script
    public GameObject heldItem;
    public HeldItem heldItemScript;
    // The item anchor gameobject
    public GameObject itemAnchor;
    // The parent of the held-item anchor we need to move up/down when crouching
    Transform itemAnchorParent;
    Vector3 itemAnchorParentPos;

    // Public access for the role
    public TartRole Role { get => _role; }

    // ID is a alias for actorNumber
    public int ID { get => actorNumber; }
    public bool IsDead { 
        get => _isDead;
        set
        {
            if (_isDead != value)
            {
                _isDead = value;
                if (_isDead) Die();
            }
        }
    }

    public bool IsCrouching { 
        get => _isCrouching;
        set
        {
            // Crouch or uncrouch if the value has changed
            if (_isCrouching != value)
            {
                if (value)
                {
                    _isCrouching = true;
                    // Lower the top of our head to crouching height (which is half)
                    topOfHead.transform.localPosition = standingHeadPos * 0.5f;
                    // Lower our item anchor to represent our arms dropping
                    itemAnchorParent.transform.localPosition = itemAnchorParentPos * 0.5f;
                } else {
                    _isCrouching = false;
                    // Raise the top of our head to standing height
                    topOfHead.transform.localPosition = standingHeadPos;
                    // Raise our item anchor
                    itemAnchorParent.transform.localPosition = itemAnchorParentPos;
                }
            }
            
        }
    }

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

        // Set our standing head height to whatever it is when we spawned
        standingHeadPos = topOfHead.transform.localPosition;
        // Set our standing itemAnchorHeight
        itemAnchorParent = itemAnchor.transform.parent;
        itemAnchorParentPos = itemAnchorParent.transform.localPosition;

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

        // Parent self to gm's spawned-players object for cleanliness
        this.transform.parent = gm.playerSpawnParent;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // TODO sync bodypart damage so we can create damage visuals on specific body parts

        if (stream.IsWriting)
        {
            // We own this player: send the others our data
            stream.SendNext(oil);
            stream.SendNext(maxOil);
            stream.SendNext(IsDead);
            stream.SendNext(IsCrouching);
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
            this.IsDead = (bool)stream.ReceiveNext();
            this.IsCrouching = (bool)stream.ReceiveNext();
            this.isReady = (bool)stream.ReceiveNext();
            this.nickname = (string)stream.ReceiveNext();
            this.actorNumber = (int)stream.ReceiveNext();
            this.aim = (Vector3)stream.ReceiveNext();
        }
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
        IsDead = false;
        this._role = gm.GetRoleFromID(0);

        SetRagdoll(false);
    }

    [PunRPC]
    public void TakeDamage(int dmg, string bodyPartName)
    {
        // Don't deal with damage if we don't own this player
        if (!photonView.IsMine) return;

        // Can't take more damage if we're dead
        if (IsDead) return;

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
            Kill();
        }
    }

    // Ticks happen once a second. Why though? Is it so we reduce network sync traffic?
    // It would be cleaner to ignore this and round values for the UI.
    private float msSinceLastTick = 0;
    public void Update()
    {
        if (!photonView.IsMine) return;

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
                if (oil <= 0 && !IsDead)
                {
                    oil = 0;
                    IsDead = true;
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

    // Turns ragdoll on/off
    void SetRagdoll(bool ragdoll)
    {
        // Change the rigidbody of all of our body parts to ragdoll mode
        foreach (BodyPart bp in bodyParts)
        {
            Rigidbody rb = bp.GetComponent<Rigidbody>();
            rb.isKinematic = !ragdoll;
            rb.useGravity = ragdoll;
        }

        // Find an Animator and turn it off
        Animator ani = GetComponent<Animator>();
        if (ani) ani.enabled = !ragdoll;
    }

    // Instantly kills this player (if local)
    public void Kill()
    {
        if (!photonView.IsMine)
        {
            lm.LogError(logSrc, "Tried to Kill a non-local player");
            return;
        }
        IsDead = true;
    }

    // Called when when isdead is set to true
    void Die()
    {
        lm.Log(logSrc, $"{nickname} died.");

        SetRagdoll(true);

        // We don't need to do anything else if this isn't our player
        if (!this.photonView.IsMine) return;

        if (!isBot) gm.Alert("YOU DIED");
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
            //gm.DeathDetailsText.text = $"Killed by {source} with {method}";
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
