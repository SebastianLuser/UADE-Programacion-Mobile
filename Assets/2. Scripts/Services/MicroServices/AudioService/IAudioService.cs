using UnityEngine;

namespace Services.MicroServices.AudioService
{
    public interface IAudioService : IGameService
    {
        void PlayMusic(AudioClip p_clip, bool p_loop = true);
        void StopMusic();
        void PauseMusic();
        void ResumeMusic();
        void PlaySFX(AudioClip p_clip);
        void SetMusicVolume(float p_volume);
        void SetSFXVolume(float p_volume);
        bool IsMusicPlaying();
        void SetAudioSources(AudioSource p_musicSource, AudioSource p_sfxSource);
        void SetMasterVolume(float p_volume);
    }
}