using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;

public class UI_ShowPlayerDetails : MonoBehaviour
{
    // The player we're showing values for.
    public Character targetPlayer;
    // The health value box
    public UnityEngine.UI.Text healthValue;
    // Current health image, which scales X with health value
    public UnityEngine.UI.Image curHealthImage;
    // Max health image, used to calculate curHealthImage scale
    public UnityEngine.UI.Image maxHealthImage;
    
    // The gamemode text box
    public UnityEngine.UI.Text gamemodeText;
    // Current time image, which scales X with round time value
    public UnityEngine.UI.Image curTimeImage;
    // Max time image, used to calculate curTimeImage scale
    public UnityEngine.UI.Image maxTimeImage;

    // Damage string box
    public UnityEngine.UI.Text damageValue;
    // TODO flashing colour to bring attention to damage

    // Role name box
    public UnityEngine.UI.Text roleText;
    // Role background image
    public UnityEngine.UI.Image roleImage;

    // Gun parent element
    public GameObject gunParent;
    // Gun name box
    public UnityEngine.UI.Text gunText;
    // Bullet value box
    public UnityEngine.UI.Text gunBulletsLeft;
    // Bullet value image, scales x
    public UnityEngine.UI.Image gunCurrentBulletsImage;
    // Bullet background image, for scaling
    public UnityEngine.UI.Image gunMaxBulletsImage;


    // The healing object
    public GameObject healingObj;
    // Healing time image, which scales X with healing progress
    public UnityEngine.UI.Image curHealTimeImage;
    // Max healing time image, used to calculate curHealTimeImage scale
    public UnityEngine.UI.Image maxHealTimeImage;

    // Update is called once per frame
    void Update()
    {
        // Try to find a local player if we don't have one.
        if (!targetPlayer)
        {
            targetPlayer = gm.characters.Find(delegate (Character p)
            {
                return p && p.photonView && p.photonView.IsMine;
            });
            // If we still didn't find one, try next time.
            if (!targetPlayer) return;
        }

        // Set health value
        if (healthValue) healthValue.text = targetPlayer.oil.ToString();
        // Set health image to scale with health value
        if (curHealthImage && maxHealthImage)
        {
            Vector2 newSize = new Vector2(maxHealthImage.rectTransform.rect.width * ((float)targetPlayer.oil / (float)targetPlayer.maxOil), maxHealthImage.rectTransform.rect.height);
            curHealthImage.rectTransform.sizeDelta = newSize;
        }

        // Set healing elements
        healingObj.active = targetPlayer.isHealing;
        if (targetPlayer.isHealing)
        {
            Vector2 newSize = new Vector2(maxHealTimeImage.rectTransform.rect.width * ((float)targetPlayer.sSinceLastHeal / (float)targetPlayer.sHealInterval), maxHealTimeImage.rectTransform.rect.height);
            curHealTimeImage.rectTransform.sizeDelta = newSize;
        }

        // Set gamemode text and values
        if (gamemodeText) gamemodeText.text = gm.gamemode + "\n" + gm.CurrentGameState;
        // Scale image according to pre-round/post-round
        if (curTimeImage && maxTimeImage)
        {
            Vector2 newSize = new Vector2();
            switch (gm.CurrentGameState)
            {
                case GameState.PreRound:
                    newSize = new Vector2(maxTimeImage.rectTransform.rect.width * (1 - (gm.curPreRoundTime / gm.preRoundTime)), maxTimeImage.rectTransform.rect.height);
                    break;
                case GameState.Active:
                    newSize = new Vector2(maxTimeImage.rectTransform.rect.width, maxTimeImage.rectTransform.rect.height);
                    break;
                case GameState.PostRound:
                    newSize = new Vector2(maxTimeImage.rectTransform.rect.width * (gm.curPostRoundTime / gm.postRoundTime), maxTimeImage.rectTransform.rect.height);
                    break;
            }
            curTimeImage.rectTransform.sizeDelta = newSize;
        }

        // Set damage value. The UI shows oil change per second, so if damage>0 then oil change is negative.
        if (damageValue) damageValue.text = (targetPlayer.GetDamage() > 0 ? "-" : "") + targetPlayer.GetDamage() + "/s";

        // Set role name
        if (roleText) roleText.text = targetPlayer.Role.Name;
        if (roleImage) roleImage.color = targetPlayer.Role.ClassColour;

        // Set gun details
        if (targetPlayer.heldItem && targetPlayer.heldItemScript && targetPlayer.heldItemScript.gun)
        {
            Gun gun = targetPlayer.heldItemScript.gun;
            if (!gunParent.activeSelf) gunParent.SetActive(true);
            // Set gun name
            gunText.text = targetPlayer.heldItemScript.nickname;
            // Set bullets number
            gunBulletsLeft.text = gun.bulletsInMag.ToString();
            // Set bullet image to scale with bullets value
            if (gunCurrentBulletsImage && gunMaxBulletsImage)
            {
                Vector2 newSize = new Vector2(gunMaxBulletsImage.rectTransform.rect.width * ((float)gun.bulletsInMag / (float)gun.magazineSize), gunMaxBulletsImage.rectTransform.rect.height);
                gunCurrentBulletsImage.rectTransform.sizeDelta = newSize;
            }
        } else
        {
            gunParent.SetActive(false);
        }
    }

}
