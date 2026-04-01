namespace Vortex.Core.VideoSystem.Model
{
    public enum ScreenMode
    {
        /// <summary>
        ///   <para>Windows platforms only. Sets your application so it has sole full-screen use of a display. </para>
        /// </summary>
        ExclusiveFullScreen,

        /// <summary>
        ///   <para>All platforms. Sets your application window to the full-screen native display resolution, covering the whole screen. </para>
        /// </summary>
        FullScreenWindow,

        /// <summary>
        ///   <para>Desktop platforms only. Sets your application to a standard, movable window that's not full screen.</para>
        /// </summary>
        Windowed,

        /// <summary>
        ///   <para>Windows and macOS platforms only. Sets your application window to the operating system's definition of maximized. </para>
        /// </summary>
        MaximizedWindow,
    }
}