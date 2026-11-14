using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.AssetHandleSystem;
using Vortex.Unity.AudioSystem.Attributes;
using Vortex.Unity.AudioSystem.Model;

namespace Vortex.Sdk.CutsceneSystem.Models
{
    /// <summary>
    /// Звук катсцены: набор клипов, из которых на каждое проигрывание выбирается один, канал и механика антиповтора.
    /// Клипы лежат за <see cref="AssetHandle{T}"/>: грузит и отпускает их представление катсцены
    /// </summary>
    [Serializable]
    public class CutsceneSound
    {
        [SerializeReference] private AssetHandle<AudioClip>[] clips = new AssetHandle<AudioClip>[0];

        [SerializeField, AudioChannelName] private string channel;

        [SerializeField, Tooltip("Механика антиповтора: с последним / с последними (N/2) / без.")]
        private AntiRepeatMode antiRepeat = AntiRepeatMode.LastMany;

        public IReadOnlyList<AssetHandle<AudioClip>> Clips => clips;

        public string Channel => channel;

        public AntiRepeatMode AntiRepeat => antiRepeat;

        /// <summary>
        /// Звук не задан
        /// </summary>
        public bool IsEmpty => clips == null || clips.Length == 0;
    }
}
