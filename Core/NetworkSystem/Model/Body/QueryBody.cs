namespace Vortex.Core.NetworkSystem.Model.Body
{
    /// <summary>
    /// Тело запроса одного из пяти видов: <see cref="ObjectBody"/>, <see cref="BytesBody"/>, <see cref="TextBody"/>,
    /// <see cref="FormBody"/>, <see cref="MultipartBody"/>. Набор видов закрыт.
    /// </summary>
    public abstract class QueryBody
    {
        private protected QueryBody()
        {
        }
    }
}
