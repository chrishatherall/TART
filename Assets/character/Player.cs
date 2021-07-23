using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using tart;

public class Player : MonoBehaviourPunCallbacks, IPunObservable
{
    // TODO sync these values!

    // Role
    private TartRole _role;

    // Unique ID provided by PhotonView.
    public int id = 0;

    #region Sync'd variables
    // Oil is effectively hitpoints
    public int oil = 100;
    public int maxOil = 100;
    // Damage is how much oil is lost each tick
    public int damage = 3;
    // is the player dead
    public bool isDead = false;
    // Local player is loaded and ready to play a round
    public bool isReady = false;
    [SerializeField]
    public string nickname;
    #endregion


    // Ref to the GameManager.
    private GameManager GM; 

    // Setup flag
    public bool isSetup = false;

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
            stream.SendNext(nickname);
            stream.SendNext(isReady);
        }
        else
        {
            // Network player, receive data
            this.oil = (int)stream.ReceiveNext();
            this.maxOil = (int)stream.ReceiveNext();
            this.damage = (int)stream.ReceiveNext();
            this.isDead = (bool)stream.ReceiveNext();
            this.nickname = (string)stream.ReceiveNext();
            this.isReady = (bool)stream.ReceiveNext();
        }
    }

    public TartRole Role 
    { 
        get => _role; 
    }

    void Start()
    {
        if (isSetup)
        {
            Debug.LogWarning("[Player] Already set up!");
            return;
        }
        GM = GameManager.gm;
        // GM might not be ready yet.
        if (!GM)
        {
            Debug.LogError("[Player] GM not ready!");
        }
        // Announce self to GM.
        Debug.Log("[Player] Started. Announcing to GameManager.");
        GM.AddPlayer(this);
        // Grab our network ID
        this.id = photonView.ViewID;
        // Set ourselves as default role
        this._role = GM.GetRoleFromID(0);
        // Mark setup done.
        isSetup = true;

        // Set as ready, and apply nickname when the local player has loaded
        if (photonView.IsMine)
        {
            isReady = true;
            nickname = PhotonNetwork.LocalPlayer.NickName;
        }
    }

    [PunRPC]
    public void SetRoleById(int id)
    {
        if (!isSetup)
        {
            Debug.LogError("[Player] Cannot set role, not set up!");
            return;
        }
        _role = GM.GetRoleFromID(id);
        if (photonView.IsMine) GM.Alert("Role is now " + _role.Name + "!");
    }

    // Role is set via rpc instead of syncvar because we can't easily sync a class
    //[PunRPC]
    //void RpcSetRoleById (int id)
    //{
    //    if (!isSetup)
    //    {
    //        Debug.LogError("[Player] Cannot set role, not set up!");
    //        return;
    //    }
    //    _role = GM.GetRoleFromID(id);
    //    if (photonView.IsMine) GM.Alert("Role is now " + _role.Name + "!");
    //}

    [PunRPC]
    public void RpcReset()
    {
        oil = 100;
        maxOil = 100;
        damage = 0;
        isDead = false;
        this._role = GM.GetRoleFromID(0);
    }

    public void TakeDamage(int dmg)
    {
        if (isDead) return;

        damage += dmg;

        if (oil <= 0)
        {
            oil = 0;
            isDead = true;
            GM.Alert("DEAD");
        }
    }

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
        GM.Alert("DEAD");
    }

}
