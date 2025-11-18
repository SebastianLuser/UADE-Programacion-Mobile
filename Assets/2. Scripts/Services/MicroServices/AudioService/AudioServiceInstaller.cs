using Services;
using Services.MicroServices.AudioService;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class AudioServiceInstaller : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private AudioConfig audioConfig;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    
    private void Awake()
    {
        if (audioConfig == null || musicSource == null || sfxSource == null)
        {
            return;
        }

        var audioService = ServiceLocator.Get<IAudioService>() as AudioService;
        if (audioService != null)
        {
            audioService.Config = audioConfig;
            audioService.SetAudioSources(musicSource, sfxSource);
        }
    }
}