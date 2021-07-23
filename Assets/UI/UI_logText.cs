using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI_logText : MonoBehaviour
{
    UnityEngine.UI.Text logText;
    // Start is called before the first frame update
    void Start()
    {
        logText = GetComponent<UnityEngine.UI.Text>();
        if (logText) Application.logMessageReceived += HandleLog;
    }

    void HandleLog(string logString, string stack, LogType type)
    {
        logText.text += "\n[" + type.ToString() + "] " + logString;
        if (type == LogType.Exception)
        {
            logText.text += "\n" + stack;
        }
    }
}
