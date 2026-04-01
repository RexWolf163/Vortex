using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Vortex.Core.AudioSystem;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.AudioSystem.Model;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.AudioSystem.Model;
using Vortex.Unity.AudioSystem.Presets;
using AudioSettings = Vortex.Core.AudioSystem.Model.AudioSettings;

namespace Vortex.Unity.AudioSystem
{
    public partial class AudioDriver : Singleton<AudioDriver>, IDriver
    {
        private const string SaveKey = "AudioSettings";
        public event Action OnInit;

        private static Dictionary<string, IAudioSample> _indexSound;
        private static Dictionary<string, IAudioSample> _indexMusic;
        private static AudioSettings _settings;

        public void Init()
        {
            Database.OnInit += OnDatabaseInit;
            AudioController.OnSettingsChanged += SaveSettings;
        }

        public void Destroy()
        {
            Database.OnInit -= OnDatabaseInit;
            AudioController.OnSettingsChanged -= SaveSettings;
            TimeController.RemoveCall(this);
        }

        /// <summary>
        /// Заполнение индексов
        /// </summary>
        private void OnDatabaseInit()
        {
            Database.OnInit -= OnDatabaseInit;

            _indexSound.Clear();
            var list = Database.GetRecords<Sound>();
            foreach (var record in list)
                _indexSound.AddNew(record.GuidPreset, record);

            _indexMusic.Clear();
            var list2 = Database.GetRecords<Music>();
            foreach (var record in list2)
                _indexMusic.AddNew(record.GuidPreset, record);

            OnInit?.Invoke();
        }

        public void SetLinks(Dictionary<string, IAudioSample> indexSound,
            Dictionary<string, IAudioSample> indexMusic, AudioSettings settings)
        {
            _indexSound = indexSound;
            _indexMusic = indexMusic;
            _settings = settings;
            LoadSettings();
        }

        private static void SaveSettings()
        {
            var save = new List<string>
            {
                $"{(_settings.MasterOn ? "Y" : "N")}",
                $"{_settings.MasterVolume.ToString(CultureInfo.InvariantCulture)}",
                $"{(_settings.MusicOn ? "Y" : "N")}",
                $"{_settings.MusicVolume.ToString(CultureInfo.InvariantCulture)}",
                $"{(_settings.SoundOn ? "Y" : "N")}",
                $"{_settings.SoundVolume.ToString(CultureInfo.InvariantCulture)}"
            };

            foreach (var channel in _settings.Channels.Values)
                save.Add(channel.ToSave());

            PlayerPrefs.SetString(SaveKey, string.Join(';', save));
            PlayerPrefs.Save();
        }

        private static void LoadSettings()
        {
            var channelConfigs = Resources.LoadAll<AudioChannelsConfig>("");
            var channelConfig = channelConfigs.Length > 0 ? channelConfigs[0] : null;
            _settings.Channels.Clear();
            if (channelConfig != null)
                foreach (var channelName in channelConfig.GetChannels())
                    _settings.Channels.AddNew(channelName, new AudioChannel(channelName));
            var save = PlayerPrefs.GetString(SaveKey);
            if (save.IsNullOrWhitespace())
                return;
            var ar = save.Split(';');
            try
            {
                AudioController.SetMasterState(ar[0] == "Y");
                AudioController.SetMasterVolume(float.Parse(ar[1], CultureInfo.InvariantCulture));
                AudioController.SetMusicState(ar[2] == "Y");
                AudioController.SetMusicVolume(float.Parse(ar[3], CultureInfo.InvariantCulture));
                AudioController.SetSoundState(ar[4] == "Y");
                AudioController.SetSoundVolume(float.Parse(ar[5], CultureInfo.InvariantCulture));
                if (ar.Length <= 6)
                    return;
                ar = ar[6..];
                foreach (var s in ar)
                {
                    var data = s.Split(':');
                    var channelName = data[0];
                    if (!_settings.Channels.TryGetValue(channelName, out var channel)) continue;
                    channel.FromSave(data);
                }
            }
            catch (Exception e)
            {
                AudioController.SetMasterState(true);
                AudioController.SetMasterVolume(1);
                AudioController.SetMusicState(true);
                AudioController.SetMusicVolume(1);
                AudioController.SetSoundState(true);
                AudioController.SetSoundVolume(1);
                SaveSettings();

                Debug.LogException(e);
                Debug.LogError("[AudioDriver] Audio settings was resets.");
            }
        }
    }
}