using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;

public class UI_ShowPlayerDetails : MonoBehaviour
{
    // The player we're showing values for.
    public Player targetPlayer;
    // The health value box
    public UnityEngine.UI.Text healthValue;
    // Current health image, which scales X with health value
    public UnityEngine.UI.Image curHealthImage;
    // Max health image, used to calculate curHealthImage scale
    public UnityEngine.UI.Image maxHealthImage;

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

    // Update is called once per frame
    void Update()
    {
        // Try to find a local player if we don't have one.
        if (!targetPlayer)
        {
            targetPlayer = gm.players.Find(delegate (Player p)
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
