using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Delegate for log messages with error flag
public delegate void LogEntry(string message, bool error);

// Responsible for collecting logs and providing them to UI elements
public class LogManager : MonoBehaviour
{
    // Set static for easy reference in other scripts.
    public static LogManager lm;

    // Log events
    public event LogEntry OnLog;

    // TODO The log list might be better as a filterable array
    string log = "";

    private void Awake()
    {
        // Set static reference
        lm = this;
        // Please don't kill me
        DontDestroyOnLoad(this);
        // Add hook for Unity logs
        Application.logMessageReceived += HandleUnityLog;

        Log("LM", "Started");
    }

    void AddLog(string source, string msg, bool error = false)
    {
        string message = $"[{source.ToUpper()}] {msg}";
        log += $"\n{message}";

        OnLog?.Invoke(message, error);
    }

    public string GetFullLog()
    { return log; }

    public void Log(string source, string msg)
    { AddLog(source, msg, false); }

    public void LogError(string source, string msg)
    { AddLog(source, msg, true); }

    public void HandleUnityLog(string logString, string stack, LogType type)
    {
        // Catches a recursive error
        if (logString.Contains("UnityEngine.UI")) return;

        AddLog("Unity", logString, type == LogType.Error); 
    }


}
