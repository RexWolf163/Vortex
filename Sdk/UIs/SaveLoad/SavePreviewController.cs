using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.SaveSystem.Abstraction;

namespace Vortex.Sdk.UIs.SaveLoad
{
    public static class SavePreviewController
    {
        private const string Divider = "||";

        /// <summary>
        /// Получение превью для сейва
        /// </summary>
        /// <param name="summary"></param>
        /// <returns></returns>
        public static Texture2D GetPreview(SaveSummary summary)
        {
            var ar = summary.Name.Split(Divider);
            if (ar.Length != 2)
                return null;
            var base64 = ar[1];
            var texture = new Texture2D(1, 1);
            return !texture.Base64ToTexture(base64) ? null : texture;
        }

        /// <summary>
        /// Получение чистого имени сейва
        /// </summary>
        /// <param name="summary"></param>
        /// <returns></returns>
        public static string GetSavePureName(this SaveSummary summary)
        {
            var ar = summary.Name.Split(Divider);
            return ar[0];
        }

        /// <summary>
        /// Формирование имени сейва с заложенными данными превью
        /// </summary>
        /// <param name="preview"></param>
        /// <param name="name"></param>
        public static string GetSaveNameWithPreview(Texture2D preview, string name) =>
            $"{name.Replace(Divider, "")}{Divider}{preview.TextureToBase64(TextureEncodingRules.JPEGMedium)}";
    }
}