using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using tart;
using static GameManager;

public class Player : MonoBehaviourPunCallbacks, IPunObservable
{
    // Role
    private TartRole _role;

    #region Sync'd variables
    // Oil is effectively hitpoints
    public int oil = 100;
    public int maxOil = 100;
    // Damage is how much oil is lost each tick
    public int damage = 0;
    // is the player dead
    public bool isDead = false;
    // Local player is loaded and ready to play a round
    public bool isReady = false;
    // Name of this player
    public string nickname;
    // The unique actor number provided by photon to networked players (alias is ID)
    public int actorNumber; 
    #endregion

    // Our held item, and script
    public GameObject heldItem;
    public HeldItem heldItemScript;
    // The item anchor gameobject
    public GameObject itemAnchor;

    // Gonna have to look into how sync/serialisation works
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this player: send the others our data
            stream.SendNext(oil);
            stream.SendNext(maxOil);
            stream.SendNext(damage);
            stream.SendNext(isDead);
            stream.SendNext(isReady);
            stream.SendNext(nickname);
            stream.SendNext(actorNumber);
        }
        else
        {
            // Network player, receive data
            this.oil = (int)stream.ReceiveNext();
            this.maxOil = (int)stream.ReceiveNext();
            this.damage = (int)stream.ReceiveNext();
            this.isDead = (bool)stream.ReceiveNext();
            this.isReady = (bool)stream.ReceiveNext();
            this.nickname = (string)stream.ReceiveNext();
            this.actorNumber = (int)stream.ReceiveNext();
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
            gm.LogError("[Player] GM not ready!");
            return;
        }
        // Announce self to GM.
        gm.Log("[Player] Started. Announcing to GameManager.");
        gm.AddPlayer(this);
        // Set ourselves as default role
        this._role = gm.GetRoleFromID(0);

        // Set as ready, and apply nickname when the local player has loaded
        if (photonView.IsMine)
        {
            nickname = PhotonNetwork.LocalPlayer.NickName;
            actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            isReady = true;
        }
    }

    [PunRPC]
    public void SetRoleById(int id)
    {
        _role = gm.GetRoleFromID(id);
        // Create an alert for the local player
        if (photonView.IsMine) gm.Alert($"Role is now {_role.Name}!");
    }

    [PunRPC]
    public void Reset()
    {
        oil = 100;
        maxOil = 100;
        damage = 0;
        isDead = false;
        this._role = gm.GetRoleFromID(0);
    }

    [PunRPC]
    public void TakeDamage(int dmg)
    {
        if (isDead) return;

        damage += dmg;

        if (oil <= 0)
        {
            oil = 0;
            isDead = true;
            gm.Alert("DEAD");
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

            // Leak oil if alive and damaged
            if (damage > 0 && !isDead)
            {
                oil -= damage;
                if (oil <= 0)
                {
                    oil = 0;
                    Die();
                }
            }
        }
    }

    // Called when something causes our death
    public void Die()
    {
        isDead = true;
        gm.Alert("DEAD");
    }

}
