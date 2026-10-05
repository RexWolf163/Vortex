namespace Vortex.Sdk.ContentTagsSystem.Model
{
    /// <summary>Как сопоставляется список тегов компонента с активным набором издания.</summary>
    public enum TagMatchMode
    {
        /// <summary>Активны все перечисленные теги.</summary>
        All = 0,

        /// <summary>Активен хотя бы один.</summary>
        Any = 1,

        /// <summary>Не активен ни один.</summary>
        None = 2,

        /// <summary>Активен ровно один из перечисленных.</summary>
        Single = 3
    }
}
