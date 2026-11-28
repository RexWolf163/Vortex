using System.Collections.Generic;
using System.Linq;

namespace Vortex.Core.NetworkSystem.Model.Body
{
    /// <summary>Тело multipart: упорядоченный список частей.</summary>
    public sealed class MultipartBody : QueryBody
    {
        private readonly MultipartPart[] _parts;

        /// <summary>Части в порядке отправки.</summary>
        public IReadOnlyList<MultipartPart> Parts => _parts;

        /// <summary>Создать тело multipart.</summary>
        /// <param name="parts">Части; список копируется.</param>
        public MultipartBody(IEnumerable<MultipartPart> parts)
        {
            _parts = parts.ToArray();
        }
    }
}
