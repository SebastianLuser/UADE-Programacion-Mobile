using System;
using UnityEngine;

public static class Logger
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    private static bool IsLoggingEnabled
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }
    }

    public static void Log(object message, LogLevel level = LogLevel.Info)
    {
        if (!IsLoggingEnabled) return;

        string formattedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
        
        switch (level)
        {
            case LogLevel.Debug:
            case LogLevel.Info:
                UnityEngine.Debug.Log(formattedMessage);
                break;
            case LogLevel.Warning:
                UnityEngine.Debug.LogWarning(formattedMessage);
                break;
            case LogLevel.Error:
                UnityEngine.Debug.LogError(formattedMessage);
                break;
        }
    }

    public static void LogDebug(object message)
    {
        Log(message, LogLevel.Debug);
    }

    public static void LogInfo(object message)
    {
        Log(message, LogLevel.Info);
    }

    public static void LogWarning(object message)
    {
        Log(message, LogLevel.Warning);
    }

    public static void LogError(object message)
    {
        Log(message, LogLevel.Error);
    }

    public static void LogException(Exception exception)
    {
        if (!IsLoggingEnabled) return;
        UnityEngine.Debug.LogException(exception);
    }
}