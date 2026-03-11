using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace SubiektNexoConsole.Bootstrap
{
    public static class NexoAssemblyResolver
    {
        public static void Register(string assemblyDirectory)
        {
            if (string.IsNullOrWhiteSpace(assemblyDirectory))
                throw new ArgumentException("Ścieżka do katalogu assembly nie może być pusta.", nameof(assemblyDirectory));

            AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
            {
                var candidatePath = Path.Combine(assemblyDirectory, $"{assemblyName.Name}.dll");

                if (!File.Exists(candidatePath))
                    return null;

                return context.LoadFromAssemblyPath(candidatePath);
            };
        }
    }
}