using UnityEngine;
using UnityEngine.Audio;

namespace Services.MicroServices.AudioService
{
    public class AudioService : IAudioService
    {
        private const string MASTER_VOLUME_PARAM = "MasterVolume";
        private const string MUSIC_VOLUME_PARAM = "MusicVolume";
        private const string SFX_VOLUME_PARAM = "SFXVolume";

        private AudioSource m_musicSource;
        private AudioSource m_sfxSource;
        
        public AudioConfig Config { get; set; }

        public void Initialize()
        {
        }
        public void SetAudioSources(AudioSource p_musicSource, AudioSource p_sfxSource)
        {
            m_musicSource = p_musicSource;
            m_sfxSource = p_sfxSource;
        }

        public void PlayMusic(AudioClip p_clip, bool p_loop = true)
        {
            if (p_clip == null || m_musicSource == null) return;
            if (m_musicSource.clip == p_clip && m_musicSource.isPlaying) return;

            m_musicSource.clip = p_clip;
            m_musicSource.loop = p_loop;
            m_musicSource.Play();
        }

        public void StopMusic()
        {
            if (m_musicSource != null)
                m_musicSource.Stop();
        }

        public void PauseMusic()
        {
            if (m_musicSource != null)
                m_musicSource.Pause();
        }

        public void ResumeMusic()
        {
            if (m_musicSource != null)
                m_musicSource.UnPause();
        }

        public void PlaySFX(AudioClip p_clip)
        {
            if (p_clip == null || m_sfxSource == null) return;
            m_sfxSource.PlayOneShot(p_clip);
        }

        public bool IsMusicPlaying()
        {
            return m_musicSource != null && m_musicSource.isPlaying;
        }

        public void SetMusicVolume(float p_volume)
        {
            if (Config?.audioMixer == null)
            {
                if (m_musicSource != null)
                    m_musicSource.volume = Mathf.Clamp01(p_volume);
                return;
            }

            SetMixerVolume(MUSIC_VOLUME_PARAM, p_volume);
        }

        public void SetSFXVolume(float p_volume)
        {
            if (Config?.audioMixer == null)
            {
                if (m_sfxSource != null)
                    m_sfxSource.volume = Mathf.Clamp01(p_volume);
                return;
            }

            SetMixerVolume(SFX_VOLUME_PARAM, p_volume);
        }

        public void SetMasterVolume(float p_volume)
        {
            if (Config?.audioMixer == null) return;
            SetMixerVolume(MASTER_VOLUME_PARAM, p_volume);
        }

        private void SetMixerVolume(string p_parameterName, float p_normalizedVolume)
        {
            if (Config?.audioMixer == null) return;

            float volume = Mathf.Clamp01(p_normalizedVolume);
            float db = volume > 0.0001f ? 20f * Mathf.Log10(volume) : -80f;
            
            Config.audioMixer.SetFloat(p_parameterName, db);
        }
    }
}