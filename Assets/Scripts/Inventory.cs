using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    List<string> items;

    // Start is called before the first frame update
    void Start()
    {
        items = new List<string>();
    }


    public void AddItem(string item)
    {
        items.Add(item);
    }

    public bool HasItem(string item)
    {
        return items.Find(i => i==item) != null;
    }
}
