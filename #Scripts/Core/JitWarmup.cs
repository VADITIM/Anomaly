using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Anomaly;

// .NET compiles a method the first time it runs. The first enemy of a kind to stream in, the first fight, the first
// menu would each stall a frame while their code compiles on the main thread. At startup this compiles every method of
// the game on a background thread instead, long before most of them are needed.
public static class JitWarmup
{
    private const BindingFlags ALL = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool started;

    public static void Start()
    {
        if (started)
            return;
        started = true;
        Task.Run(() =>
        {
            Type[] types;
            try
            {
                types = typeof(JitWarmup).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = Array.FindAll(exception.Types, type => type != null);
            }
            foreach (Type type in types)
            {
                if (type.ContainsGenericParameters)
                    continue;
                foreach (MethodInfo method in type.GetMethods(ALL))
                    Prepare(method);
                foreach (ConstructorInfo constructor in type.GetConstructors(ALL))
                    Prepare(constructor);
            }
        });
    }

    private static void Prepare(MethodBase method)
    {
        if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null)
            return;
        try
        {
            RuntimeHelpers.PrepareMethod(method.MethodHandle);
        }
        catch (Exception)
        {
            // A method that cannot be prepared ahead simply compiles on first use, as it would have anyway.
        }
    }
}
