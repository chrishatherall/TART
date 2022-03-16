using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using static GameManager;

// Should be added on anything that can be spawned, either by a spawn point or by a player via drop/throw

public class Spawnable : MonoBehaviour, IPunInstantiateMagicCallback
{
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
      
        // Ignore collision between the new item and the player who threw it. Allows us to spawn the item inside our player collider, which
        // ensures we can't throw items through walls.
        Collider itemCol = GetComponent<Collider>();
        Character p = gm.GetCharacterById(info.photonView.OwnerActorNr);
        if (itemCol && p)
        {
            foreach (BodyPart bp in p.bodyParts)
            {
                if (!bp.collider) return;
                Physics.IgnoreCollision(itemCol, bp.collider);
            }
            CharacterController charCon = p.GetComponent<CharacterController>();
            if (charCon) Physics.IgnoreCollision(itemCol, charCon);
        }

    }

}
