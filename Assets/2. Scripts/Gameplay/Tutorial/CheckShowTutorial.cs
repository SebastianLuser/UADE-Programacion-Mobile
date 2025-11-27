using UnityEngine;

[DefaultExecutionOrder(1000)]
public class CheckShowTutorial : MonoBehaviour
{
    private const string TutorialSeenKey = "TutorialSeen";
    
    public static bool HasSeenTutorial()
    {
        return PlayerPrefs.GetInt(TutorialSeenKey, 0) == 1;
    }

    public static void ResetTutorial()
    {
        PlayerPrefs.DeleteKey(TutorialSeenKey);
    }
    
    void Awake()
    {
        if (HasSeenTutorial())
        {
            this.gameObject.SetActive(false);
        }
    }
}
