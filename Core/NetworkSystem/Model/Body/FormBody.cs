using System.Collections.Generic;
using System.Linq;

namespace Vortex.Core.NetworkSystem.Model.Body
{
    /// <summary>Тело-форма: поля «имя → значение» в порядке добавления.</summary>
    public sealed class FormBody : QueryBody
    {
        private readonly KeyValuePair<string, object>[] _fields;

        /// <summary>Поля формы «имя → значение» в порядке добавления.</summary>
        public IReadOnlyList<KeyValuePair<string, object>> Fields => _fields;

        /// <summary>Создать тело-форму.</summary>
        /// <param name="fields">Поля формы; список копируется.</param>
        public FormBody(IEnumerable<KeyValuePair<string, object>> fields)
        {
            _fields = fields.ToArray();
        }
    }
}
