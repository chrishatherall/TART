
using ExitGames.Client.Photon;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;
using static LogManager;
using TART;
using Photon.Pun;

// Used in a Deathmatch game to track DM-specific values

public class DeathmatchPlayer : MonoBehaviour, IOnEventCallback
{
    //readonly string logSrc = "DM_PLAYER";

    // Player kills
    public int kills;

    // Special weapon trackers
    [SerializeField]
    private bool hasGrenade;
    [SerializeField]
    private bool hasC4;

    public int grenadePoints;
    public int grenadeMaxPoints = 3;
    public int c4Points;
    public int c4MaxPoints = 10;

    // Ref to the attached Player script
    Player player;

    // The audio clip played when we get an upgrade
    [SerializeField]
    AudioClip upgradeSound;

    public bool HasGrenade { 
        get => hasGrenade;
        set
        {
            if (!hasGrenade && value && upgradeSound) player.PlaySound(upgradeSound);
            hasGrenade = value;
            if (!hasGrenade) grenadePoints = 0;
            RecalculateProgressBars();
            //if (gm.grenadeNotification) gm.grenadeNotification.SetActive(value);
        }
    }
    public bool HasC4 { 
        get => hasC4;
        set
        {
            if (!hasC4 && value && upgradeSound) player.PlaySound(upgradeSound);
            hasC4 = value;
            if (!hasC4) c4Points = 0;
            RecalculateProgressBars();
            //if (gm.c4Notification) gm.c4Notification.SetActive(value);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        player = GetComponent<Player>();

        if (!player || !player.photonView.IsMine || gm.gamemode != "Deathmatch")
        {
            this.enabled = false;
        }

        player.DMPlayer = this;

        RecalculateProgressBars();

    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == (byte)Events.CharacterDied)
        {
            // object[] args = { ID, murdererID, causeOfDeath };
            object[] data = (object[])photonEvent.CustomData;
            int deadCharacterId = (int)data[0];
            int murdererID = (int)data[1];

            // If we're the murderer and we didn't kill ourselves
            if (player.character.ID != deadCharacterId && murdererID == player.character.ID)
            {
                grenadePoints++;
                c4Points++;
                kills++;

                if (grenadePoints >= grenadeMaxPoints)
                {
                    grenadePoints = grenadeMaxPoints;
                    HasGrenade = true;
                }

                if (c4Points >= c4MaxPoints)
                {
                    c4Points = c4MaxPoints;
                    HasC4 = true;
                }

                RecalculateProgressBars();
            }
        }

    }

    void RecalculateProgressBars()
    {
        if (!player.photonView.IsMine) return;

        if (gm.curGrenadePointsImage && gm.maxGrenadePointsImage)
        {
            Vector2 newSize = new Vector2(gm.maxGrenadePointsImage.rectTransform.rect.width * ((float)grenadePoints / (float)grenadeMaxPoints), gm.maxGrenadePointsImage.rectTransform.rect.height);
            gm.curGrenadePointsImage.rectTransform.sizeDelta = newSize;
        }
        if (gm.curC4PointsImage && gm.maxC4PointsImage)
        {
            Vector2 newSize = new Vector2(gm.maxC4PointsImage.rectTransform.rect.width * ((float)c4Points / (float)c4MaxPoints), gm.maxC4PointsImage.rectTransform.rect.height);
            gm.curC4PointsImage.rectTransform.sizeDelta = newSize;
        }

        if (gm.killCountText) gm.killCountText.text = $"{kills} K";
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    // Update is called once per frame for the owner
    void Update()
    {
        // Track input for these special weapons
        if (HasGrenade && Input.GetKeyDown("f"))
        {
            // Throw grenade
            if (player && player.character && player.character.TryDropItem("Grenade")) HasGrenade = false;
        }

        if (HasC4 && Input.GetKeyDown("c"))
        {
            // Place c4
            if (player && player.character && player.character.TryPlaceItem("C4", 2f)) HasC4 = false;
        }
    }

    public void Reset(bool forceRespawn)
    {
        kills = 0;
        c4Points = 0;
        grenadePoints = 0;
        RecalculateProgressBars();
    }
}
