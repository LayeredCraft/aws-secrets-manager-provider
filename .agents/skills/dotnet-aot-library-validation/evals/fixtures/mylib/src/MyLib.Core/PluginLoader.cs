using System;
using System.Collections.Generic;
using System.Reflection;

namespace MyLib.Core;

/// <summary>Discovers and instantiates plugin types via reflection.</summary>
public static class PluginLoader
{
    public static IEnumerable<IPlugin> LoadPlugins(Assembly assembly, Type markerType)
    {
        var results = new List<IPlugin>();
        foreach (var type in assembly.GetTypes())
        {
            if (!markerType.IsAssignableFrom(type) || type.IsAbstract)
            {
                continue;
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                // inspect plugin surface
                _ = method.GetParameters();
            }

            results.Add((IPlugin)Activator.CreateInstance(type)!);
        }
        return results;
    }
}

public interface IPlugin
{
    string Name { get; }
    void Execute();
}
