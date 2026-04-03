using System;
using UnityEngine;

namespace Vortex.Core.Extensions.LogicExtensions
{
    /// <summary>
    /// Расширение функционала работы с текстурами
    /// </summary>
    public static class TextureExtBase64
    {
        /// <summary>
        /// Кодирование текстуры в строку
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="encodingRules"></param>
        /// <returns></returns>
        public static string TextureToBase64(this Texture2D texture,
            TextureEncodingRules encodingRules = TextureEncodingRules.PNG)
        {
            try
            {
                var bytes = EncodeTexture(texture, encodingRules);
                return Convert.ToBase64String(bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TextureConverter] TextureToBase64 failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Восстановление Текстуры из строки
        /// </summary>
        /// <param name="texture">Текстура под заливку данными</param>
        /// <param name="base64"></param>
        /// <returns>TRUE при успешном перекодировании</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        public static bool Base64ToTexture(this Texture2D texture, string base64)
        {
            try
            {
                if (string.IsNullOrEmpty(base64))
                    throw new ArgumentException("Base64 string is null or empty");

                var bytes = Convert.FromBase64String(base64);

                if (!texture.LoadImage(bytes))
                    throw new Exception("Failed to load image from bytes");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TextureConverter] Base64ToTexture failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Перевод текстуры в форму byte[] по указанным правилам сжатия 
        /// </summary>
        /// <param name="texture">Исходная текстура</param>
        /// <param name="encodingRules">Правила кодирования</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private static byte[] EncodeTexture(Texture2D texture, TextureEncodingRules encodingRules)
        {
            if (texture == null)
                throw new ArgumentNullException(nameof(texture));

            return encodingRules switch
            {
                TextureEncodingRules.PNG => texture.EncodeToPNG(),
                TextureEncodingRules.JPEGLow => texture.EncodeToJPG(25),
                TextureEncodingRules.JPEGMedium => texture.EncodeToJPG(50),
                TextureEncodingRules.JPEGHigh => texture.EncodeToJPG(75),
                TextureEncodingRules.JPEGMax => texture.EncodeToJPG(100),
                _ => throw new ArgumentOutOfRangeException(nameof(encodingRules), $"Unknown encoding: {encodingRules}")
            };
        }
    }
}