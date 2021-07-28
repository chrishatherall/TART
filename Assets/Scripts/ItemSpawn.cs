using Photon.Realtime;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ExitGames.Client.Photon;
using static GameManager;

public class ItemSpawn : MonoBehaviour
{
    [SerializeField]
    string spawnList; // This should match the name of the Object containing SpawnListItem components.

    // Called by the GameManager on preround
    public void SpawnItem()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        // Find a random item from our list
        GameObject prefab = gm.GetItemFromSpawnList(spawnList);
        gm.Log("Spawning item: " + prefab.name);
        PhotonNetwork.Instantiate(prefab.name, this.transform.position, Quaternion.identity, 0);
    }
}
