using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;
using static LogManager;

// Used in a Deathmatch game to track DM-specific values

public class DeathmatchPlayer : MonoBehaviour
{
    //readonly string logSrc = "DM_PLAYER";

    // Player kills
    public int kills;

    // Special weapon trackers
    private bool hasGrenade;
    private bool hasC4;

    // Ref to the attached player script
    Player p;

    // The audio clip played when we get an upgrade
    [SerializeField]
    AudioClip upgradeSound;

    // Temporarily give upgrades over time instead of on X kills
    float sTimeUntilUpgrade = 0f;
    [SerializeField]
    float sTimeBetweenUpgrades = 10f;


    public bool HasGrenade { 
        get => hasGrenade;
        set
        {
            if (!hasGrenade && value && upgradeSound) p.audioSrc.PlayOneShot(upgradeSound);
            hasGrenade = value;
            if (gm.grenadeNotification) gm.grenadeNotification.SetActive(value);
        }
    }
    public bool HasC4 { 
        get => hasC4;
        set
        {
            if (!hasC4 && value && upgradeSound) p.audioSrc.PlayOneShot(upgradeSound);
            hasC4 = value;
            if (gm.c4Notification) gm.c4Notification.SetActive(value);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        p = GetComponent<Player>();

        if (!p || !p.photonView.IsMine || gm.gamemode != "Deathmatch")
        {
            this.enabled = false;
        }

        sTimeUntilUpgrade = sTimeBetweenUpgrades;
    }


    // Update is called once per frame for the owner
    void Update()
    {
        // Until there's a way to track cause of death (and assign kills), just get these upgrades over time.
        sTimeUntilUpgrade -= Time.deltaTime;
        if (sTimeUntilUpgrade < 0f)
        {
            if (!HasGrenade)
            {
                HasGrenade = true;
            } else
            {
                HasC4 = true;
            }
            sTimeUntilUpgrade = sTimeBetweenUpgrades;
        }

        // Track input for these special weapons
        if (HasGrenade && Input.GetKeyDown("f"))
        {
            // Throw grenade
            FpsController fpsc = GetComponent<FpsController>();
            if (fpsc)
            {
                fpsc.TryDropItem("Grenade");
                HasGrenade = false;
            }
        }

        if (HasC4 && Input.GetKeyDown("c"))
        {
            // Place c4
            FpsController fpsc = GetComponent<FpsController>();
            if (fpsc && fpsc.CanPlaceItem("C4", 2f))
            {
                fpsc.PlaceItem("C4");
                HasC4 = false;
            }
        }
    }
}
