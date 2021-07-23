using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI_alert : MonoBehaviour
{
    public int secondsAlive = 5;

    public void Start()
    {
        Destroy(this.gameObject, secondsAlive);
    }

    public void SetMessage(string message)
    {
        GetComponent<UnityEngine.UI.Text>().text = message;
    }
}
