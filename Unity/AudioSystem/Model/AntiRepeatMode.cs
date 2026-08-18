namespace Vortex.Unity.AudioSystem.Model
{
    /// <summary>
    /// Механика антиповтора при случайном выборе клипа в <see cref="SoundClip"/>.
    /// </summary>
    public enum AntiRepeatMode
    {
        /// <summary>Сравнение с последними: не выдавать N/2 (округляя вниз) недавно проигранных клипов.</summary>
        LastMany = 0,

        /// <summary>Сравнение с последним: не выдавать один предыдущий клип.</summary>
        LastOne = 1,

        /// <summary>Без антиповтора: честный равномерный рандом.</summary>
        None = 2,
    }
}
