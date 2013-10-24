namespace GitScc.PlatformUI
{
    using System;
    using System.Collections.Concurrent;
    using System.Reflection;

    public class PlatformColorHelper
    {
        private static readonly ConcurrentDictionary<Type, PlatformColorHelper> _helpers =
            new ConcurrentDictionary<Type, PlatformColorHelper>();

        private readonly ConcurrentDictionary<string, Func<object>> _propertyAccessors =
            new ConcurrentDictionary<string, Func<object>>();
        private readonly Type _platformType;

        private PlatformColorHelper(Type platformType)
        {
            if (platformType == null)
                throw new ArgumentNullException("platformType");

            _platformType = platformType;
        }

        public static object GetResourceKey(Type type, string resourceName)
        {
            PlatformColorHelper helper = _helpers.GetOrAdd(type, LookupPlatformColorHelper);
            if (helper == null)
                return null;

            return helper.GetResourceKey(resourceName);
        }

        private object GetResourceKey(string resourceName)
        {
            Func<object> accessor = _propertyAccessors.GetOrAdd(resourceName, CreatePropertyAccessor);
            if (accessor == null)
                return null;

            return accessor();
        }

        private static PlatformColorHelper LookupPlatformColorHelper(Type wrapperType)
        {
            // first try to find the Visual Studio 2013 assembly
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                AssemblyName name = assembly.GetName();
                if (!name.Name.Equals("Microsoft.VisualStudio.Shell.12.0"))
                    continue;

                Type type = assembly.GetType("Microsoft.VisualStudio.PlatformUI." + wrapperType.Name, false);
                if (type == null)
                    continue;

                return new PlatformColorHelper(type);
            }

            // fall back to the Visual Studio 2012 assembly
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                AssemblyName name = assembly.GetName();
                if (!name.Name.Equals("Microsoft.VisualStudio.Shell.11.0"))
                    continue;

                Type type = assembly.GetType("Microsoft.VisualStudio.PlatformUI." + wrapperType.Name, false);
                if (type == null)
                    continue;

                return new PlatformColorHelper(type);
            }

            return null;
        }

        private Func<object> CreatePropertyAccessor(string propertyName)
        {
            PropertyInfo property = _platformType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (property == null)
                return null;

            MethodInfo getter = property.GetGetMethod();
            if (getter == null)
                return null;

            Func<object> accessor = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), getter);
            return accessor;
        }
    }
}
