using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.SaveSystem.Abstraction;

namespace Vortex.Sdk.UIs.SaveLoad.Models
{
    /// <summary>
    /// Враппер для сейва, чтобы передавать ссылки через IDataStorage
    /// </summary>
    public class SaveSlotData
    {
        private const string SavePreviewKey = "SavePreview";

        public SaveSummary Summary { get; }

        private Texture2D _preview;

        public Texture2D Preview => _preview ??= GetPreview();
        public string Guid { get; }

        public SaveSlotData(string guid, SaveSummary summary)
        {
            Guid = guid;
            Summary = summary;
        }

        private Texture2D GetPreview()
        {
            if (!PlayerPrefs.HasKey(GetSaveKey()))
                return null;
            var texture = new Texture2D(1, 1);
            return texture;
        }

        /// <summary>
        /// Ключ сохранения картинки-превью для данного сохранения
        /// </summary>
        /// <returns></returns>
        private string GetSaveKey()
        {
            if (Summary.Name.IsNullOrWhitespace())
                return null;

            return $"{Summary.Name}_{Summary.UnixTimestamp}";
        }
    }
}