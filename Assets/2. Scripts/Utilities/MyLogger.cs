using System;
using UnityEngine;

public static class MyLogger
{
    public static void LogInfo(object message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        //MyLogger.LogInfo($"[{DateTime.Now:HH:mm:ss}] {message}");
#endif
    }

    public static void LogWarning(object message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        //MyLogger.LogWarning($"[{DateTime.Now:HH:mm:ss}] {message}");
#endif
    }

    public static void LogError(object message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        //MyLogger.LogError($"[{DateTime.Now:HH:mm:ss}] {message}");
#endif
    }

    public static void LogDebug(object message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        //MyLogger.LogInfo($"[DEBUG] [{DateTime.Now:HH:mm:ss}] {message}");
#endif
    }

    public static void LogException(Exception exception)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        //MyLogger.LogInfoException(exception);
#endif
    }
}
