namespace Vortex.Core.System.Abstractions
{
    public abstract class Singleton<T> where T : Singleton<T>, new()
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new T();
                    _instance?.OnInstantiate();
                }

                return _instance;
            }
        }

        protected static void Dispose()
        {
            _instance?.OnDispose();
            _instance = null;
        }

        protected virtual void OnInstantiate()
        {
        }

        protected virtual void OnDispose()
        {
        }
    }
}