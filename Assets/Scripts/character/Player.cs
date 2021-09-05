using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;
using TART;
using static GameManager;
using static LogManager;

// Delegate signature for empty events // TODO no idea if I actually need this
public delegate void EmptyEvent();

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
    public GameObject topOfHead;
    Vector3 standingHeadPos;
    // Movement values for animation controller
    bool _isGrounded;
    public float frontBackMovement;
    public float leftRightMovement;
    bool _isCrouching;
    public bool isMoving;
    public bool isRunning;
    public float crouchHeightMultiplier = 0.66f;
    // Velocity 
    float[] last3FramesVelocity = { 1f, 1f, 1f };
    CharacterController charCon;

    // Footsteps
    [SerializeField]
    float footstepInterval = 0.5f; // Time between footsteps
    float sLastFootstep; // Seconds since last footstep TODO not technically true, change this along with sfootsteplastplayed
    [SerializeField]
    float fFootstepVolume = 0.3f;
    [SerializeField]
    float sFootstepCooldown = 0.2f; // Time in seconds between max footstep sounds
    float sFootstepLastPlayed;
    float maxFootstepVolume = 10f;
    float minFootstepVolume = 0.2f;

    // Jump event
    public event EmptyEvent OnJump;

    // Our body parts
    public BodyPart[] bodyParts;

    // Our held item, and script
    public GameObject heldItem;
    public HeldItem heldItemScript;
    // The item anchor gameobject
    public GameObject itemAnchor;

    public AudioSource audioSrc;
    [SerializeField]
    AudioClip healSound;

    [SerializeField]
    AudioClip damageSound;

    [SerializeField]
    AudioClip deathSound;

    [SerializeField]
    AudioClip throwSound;

    // Ref to the skinned mesh renderer. Disabled locally if alive, otherwise enabled
    [SerializeField]
    SkinnedMeshRenderer bodySkinnedMesh;

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
                // Disable the skinned mesh if we're the owner and alive, otherwise enable it
                if (!isBot && bodySkinnedMesh) bodySkinnedMesh.enabled = !(photonView.IsMine && !_isDead);
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
                    topOfHead.transform.localPosition = standingHeadPos * crouchHeightMultiplier;
                } else {
                    _isCrouching = false;
                    // Raise the top of our head to standing height
                    topOfHead.transform.localPosition = standingHeadPos;
                }
            }
            
        }
    }

    public bool IsGrounded { 
        get => _isGrounded;
        set
        {
            // If the value changed and we're now grounded, do loud footstep
            if (_isGrounded != value && value)
            {
                DoFootstep(last3FramesVelocity[2]);
            }
            _isGrounded = value;
        }
    }

    // Healing
    public bool isHealing;
    public float sSinceLastHeal = 0f;
    public float sHealInterval = 1f;

    // Cause of death
    bool diedToInstantDeath = false;
    int instantDeathPlayer = -1;

    void Awake()
    {
        // GM might not be ready yet.
        if (!gm)
        {
            lm.LogError(logSrc,"GM not ready!");
            return;
        }

        charCon = GetComponent<CharacterController>();

        // Set ourselves as default role
        this._role = gm.GetRoleFromID(0);

        // Find our body parts
        bodyParts = GetComponentsInChildren<BodyPart>();

        // Find our audio source
        audioSrc = GetComponent<AudioSource>();

        // Set our standing head height to whatever it is when we spawned
        standingHeadPos = topOfHead.transform.localPosition;

        // Set ID/actorNumber to the same as our controlling player number
        actorNumber = photonView.ControllerActorNr;

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
                // Turn off our skinned mesh renderer
                if (bodySkinnedMesh) bodySkinnedMesh.enabled = false;

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

            stream.SendNext(frontBackMovement);
            stream.SendNext(leftRightMovement);
            stream.SendNext(isMoving);
            stream.SendNext(isRunning);
            stream.SendNext(IsGrounded);
            stream.SendNext(SerialiseBodyPartDamage());
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

            this.frontBackMovement = (float)stream.ReceiveNext();
            this.leftRightMovement = (float)stream.ReceiveNext();
            this.isMoving = (bool)stream.ReceiveNext();
            this.isRunning = (bool)stream.ReceiveNext();
            this.IsGrounded = (bool)stream.ReceiveNext();
            DeserialiseBodyPartDamage((string)stream.ReceiveNext());
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
    public void Reset(bool forceRespawn)
    {
        // Set new position if dead or being forced to respwn
        if (IsDead || forceRespawn)
        {
            // Turn off the character controller before force-moving, or it'll just set us right back.
            CharacterController charCon = GetComponent<CharacterController>();
            bool charConWasEnabled = charCon && charCon.enabled;
            if (charConWasEnabled) charCon.enabled = false;
            Transform newTransform = gm.GetPlayerSpawnLocation();
            this.transform.position = newTransform.position;
            this.transform.rotation = newTransform.rotation;
            if (charConWasEnabled) charCon.enabled = true;
        }
        oil = 100;
        maxOil = 100;
        // Set damage of each BodyPart to 0
        foreach(BodyPart bp in bodyParts) { bp.Reset(); }
        IsDead = false;
        this._role = gm.GetRoleFromID(0);

        SetRagdoll(false);

        diedToInstantDeath = false;
        instantDeathPlayer = -1;

        // Kind of a dirty fix, but works until we have a UI manager.
        if (photonView.IsMine) Cursor.lockState = CursorLockMode.Locked;
    }

    // Called from a remote/local BodyPart script when it is damaged.
    [PunRPC]
    public void DamageBone(string bodyPartNamesString, int dmg, Vector3 hitDirection, int sourcePlayerID)
    {
        lm.Log(logSrc, $"Taking damage of {dmg} to bodyparts {bodyPartNamesString}");
        // Don't deal with damage if we don't own this player
        //if (!photonView.IsMine) return; // TODO TEMPORARILY do this on all clients for the visuals, need proper BodyPart syncing to avoid doing this one all clients

        // Can't take more damage if we're dead
        if (IsDead) return;

        // Play damage sound if we own this player
        if (photonView.IsMine) audioSrc.PlayOneShot(damageSound);

        // Split bodypart string into list of BodyParts
        string[] bodyPartStrings = bodyPartNamesString.Split('/');
        List<BodyPart> bodyParts = new List<BodyPart>();
        foreach (string bps in bodyPartStrings)
        {
            BodyPart bp = GetBodyPartByName(bps);
            if (bp)
            {
                bodyParts.Add(bp);
                // Apply damage to the BodyPart
                //bp.Damage += dmg; // here we would call a method and pass damage+playerid. This would allow special bodyparts to adjust damage.
                bp.AddDamage(dmg, sourcePlayerID);
            }
        }

        // To make damage more responsive, kill player instantly if damage > oil, and hit the ragdoll with hitDirection force
        if (GetDamage() > oil)
        {
            Kill(hitDirection, bodyParts, sourcePlayerID);
        }
    }

    // This is to allow the server to kill a player easily
    [PunRPC]
    public void InstaKill(int dmg)
    {
        DamageBone("B-head", dmg, Vector3.up, -1); // Source of the damage is player -1, essentially nobody
    }

    // TODO only heals 1 at a time. Ideally should distribute the healing to all BPs that need it, ratiod
    public void Heal(int amount)
    {
        // Run loop once for each heal amount
        int healedAmount = 0;
        for (int i = 0; i < amount; i++)
        {
            // Get a bodypart with damage
            BodyPart bp = bodyParts.FirstOrDefault(bp => bp.CurrentDamage > 0);
            if (!bp) break;
            // Reduce damage by 1
            bp.RemoveDamage(1);
            healedAmount++;
        }

        // Do visuals if we healed anything
        if (healedAmount > 0) photonView.RPC("DoHealVisuals", RpcTarget.All);
    }

    void DoFootstep(float volumeMultiplier)
    {
        // Ensure volume is positive
        if (volumeMultiplier < 0) volumeMultiplier *= -1;
        // Ignore small values
        if (volumeMultiplier < minFootstepVolume) return;
        // Limit volume
        if (volumeMultiplier > maxFootstepVolume) volumeMultiplier = maxFootstepVolume;
        // Make sure our last footstep is over our cooldown
        if (Time.time - sFootstepLastPlayed < sFootstepCooldown) return;
        sLastFootstep = 0f;
        // Cast a ray down at the thing under our feet
        bool hit = Physics.Raycast(this.transform.position, Vector3.down, out RaycastHit footstepHit, 1f);
        if (!hit) return;
        if (audioSrc) audioSrc.PlayOneShot(gm.GetFootstepByMaterial(footstepHit.collider.material), fFootstepVolume * volumeMultiplier);
        sFootstepLastPlayed = Time.time;
    }

    // Called on all clients by the fpscontroller
    [PunRPC]
    public void Jump()
    {
        // Invoke the jump event
        OnJump.Invoke();
    }

    [PunRPC]
    public void DoHealVisuals()
    {
        // Play sound
        if (audioSrc && healSound) audioSrc.PlayOneShot(healSound);
    }

    [PunRPC]
    public void DoThrowVisuals()
    {
        // Play sound
        if (audioSrc && throwSound) audioSrc.PlayOneShot(throwSound);
    }

    // Ticks happen once a second. Why though? Is it so we reduce network sync traffic?
    // It would be cleaner to ignore this and round values for the UI.
    private float msSinceLastTick = 0;
    public void Update()
    {
        // Footsteps
        if (!IsDead && IsGrounded && isRunning)
        {
            sLastFootstep += Time.deltaTime;
            if (sLastFootstep > footstepInterval)
            {
                DoFootstep(1);
            }
        }
        if (charCon)
        {
            last3FramesVelocity[2] = last3FramesVelocity[1];
            last3FramesVelocity[1] = last3FramesVelocity[0];
            last3FramesVelocity[0] = charCon.velocity.y;
        }

        if (!photonView.IsMine) return;

        // Track healing time
        if (isHealing && !IsDead)
        {
            sSinceLastHeal += Time.deltaTime;
            if (sSinceLastHeal > sHealInterval)
            {
                Heal(5);
                sSinceLastHeal = 0f;
            }
        } else {
            sSinceLastHeal = 0f;
        }

        // Track DOT
        msSinceLastTick += Time.deltaTime;
        if (msSinceLastTick > 1) // One second
        {
            msSinceLastTick = 0;

            int dmg = GetDamage();

            // Leak oil if damaged
            if (dmg > 0 && oil > 0)
            {
                if (!IsDead) audioSrc.PlayOneShot(damageSound);
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
        return bodyParts.FirstOrDefault(bp => bp.gameObject.name == name);
    }

    // Serialises the damage of our BodyParts into a string
    string SerialiseBodyPartDamage ()
    {
        string[] bpData = new string[bodyParts.Length];
        for (int i = 0; i < bodyParts.Length; i++)
        {
            bpData[i] = bodyParts[i].Serialise(); //bodyParts[i].gameObject.name + ":" + bodyParts[i].Damage;
        }
        return string.Join("/", bpData);
    }

    void DeserialiseBodyPartDamage (string data)
    {
        // The first frame we might not have any bones, so ignore 0-length data. BodyParts can also be null on startup
        if (data.Length == 0 || bodyParts == null || bodyParts.Length == 0) return;

        string[] parts = data.Split('/');
        foreach (string part in parts)
        {
            string[] split = part.Split(':');
            // Find a BodyPart by name of split[0]
            // bp-head:1001:4:1002:8
            BodyPart bp = GetBodyPartByName(split[0]);
            if (!bp)
            {
                lm.LogError(logSrc, $"Error deserialising BodyPart damage, invalid part name. Data: '{data}'");
                return;
            }
            bp.Deserialise(part);
        }
    }

    public int GetDamage()
    {
        return bodyParts.Sum(bp => bp.CurrentDamage);
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

        // If we're a local player we need to turn our controller off. TODO I don't like this being here but it needs to be off before we apply ragdoll
        if (charCon) charCon.enabled = !ragdoll;

        // Find an Animator and turn it off
        Animator ani = GetComponent<Animator>();
        if (ani) ani.enabled = !ragdoll;
    }

    // Instantly kills this player. This is called on all clients when something instantly kills someone, to replicate the ragdoll nicely.
    // TODO This seems messy
    public void Kill(Vector3 hitDirection, List<BodyPart> ragdollBodyParts, int playerID)
    {
        diedToInstantDeath = true;
        instantDeathPlayer = playerID;
        //if (!photonView.IsMine)
        //{
        //    lm.LogError(logSrc, "Tried to Kill a non-local player");
        //    return;
        //}
        IsDead = true;
        // Ragdoll should be enabled now that we're dead, so apply hit force
        foreach (BodyPart bp in ragdollBodyParts)
        {
            Rigidbody rb = bp.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.AddForce(hitDirection, ForceMode.Impulse);
            }
        }
    }

    // Called when when isdead is set to true
    void Die()
    {
        lm.Log(logSrc, $"{nickname} died.");

        SetRagdoll(true);

        // We don't need to do anything else if this isn't our player
        if (!this.photonView.IsMine) return;

        // Play death sound
        audioSrc.PlayOneShot(deathSound);

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
            // As you can't currently restore lost oil, the person who killed us is the person who dealt the most BP damage, unless instant killed
            string murdererName;
            string causeOfDeath;
            if (diedToInstantDeath)
            {
                Player murderer = gm.GetPlayerByID(instantDeathPlayer);
                murdererName = murderer ? murderer.nickname : "Unknown";
                causeOfDeath = "Instant"; // TODO this should be the weapon but we don't support that yet
            } else
            {
                // Calculate killer by bp damage
                // Merge all bodypart damages
                List<Damage> sumDamages = new List<Damage>();
                foreach (BodyPart bp in bodyParts)
                {
                    foreach (Damage D in bp.Damages)
                    {
                        // Find an entry in sumDamages for this player id
                        Damage existing = sumDamages.FirstOrDefault(ED => ED.SourcePlayerID == D.SourcePlayerID);
                        if (existing != null) {
                            existing.Amount += D.Amount;
                        } else
                        {
                            sumDamages.Add(D);
                        }
                    }
                }
                // Find the biggest damage dealer
                Damage murderer = sumDamages[0];
                foreach (Damage D in sumDamages)
                {
                    if (D.Amount > murderer.Amount) murderer = D;
                }
                murdererName = gm.GetPlayerByID(murderer.SourcePlayerID).nickname;
                causeOfDeath = "Oil Loss";

            }
            gm.DeathDetailsText.text = $"Killed by {murdererName} via {causeOfDeath}";

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
