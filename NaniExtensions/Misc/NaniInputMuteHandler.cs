using UnityEngine;
using Vortex.NaniExtensions.Core;

namespace Vortex.NaniExtensions.Misc
{
    /// <summary>
    /// Отключает ввод Naninovel, пока объект включён (<see cref="NaniWrapper.MuteInput"/>). Вешается на слой,
    /// который показывается на время ввода текста, рядом с заглушками карт ввода шины. Ограничения отключения —
    /// в описании <see cref="NaniWrapper.MuteInput"/>
    /// </summary>
    public class NaniInputMuteHandler : MonoBehaviour
    {
        private void OnEnable() => NaniWrapper.MuteInput(this);

        private void OnDisable() => NaniWrapper.UnmuteInput(this);
    }
}
