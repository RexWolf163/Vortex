using System;
using UnityEngine;
using Vortex.Core.SaveSystem.Abstraction;

namespace Vortex.Sdk.UIs.SaveLoad.Models
{
    /// <summary>
    /// Враппер для сейва, чтобы передавать ссылки через IDataStorage
    /// </summary>
    public class SaveSlotData : IDisposable
    {
        public SaveSummary Summary { get; }

        private Texture2D _preview;

        public Texture2D Preview => _preview ??= SavePreviewController.GetPreview(Guid);
        public string Guid { get; }

        public SaveSlotData(string guid, SaveSummary summary)
        {
            Guid = guid;
            Summary = summary;
        }

        public void Dispose()
        {
            if (_preview != null)
            {
                UnityEngine.Object.Destroy(_preview);
                _preview = null;
            }
        }
    }
}