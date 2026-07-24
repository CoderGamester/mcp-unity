using System;
using Unity.Pipeline.Editor.Authoring;
using Unity.Pipeline.Models;
using UnityEngine;

namespace McpUnity.Extensions.Commands
{
    internal static class CommandObjectResolver
    {
        public static T Resolve<T>(ObjectRef reference, string argumentName)
            where T : UnityEngine.Object
        {
            if (reference == null || reference.IsEmpty)
                throw new ArgumentException($"'{argumentName}' is required.");

            if (!ObjectResolver.TryResolve(reference, out var resolved, out var error))
                throw new ArgumentException($"Could not resolve '{argumentName}': {error}");

            if (!(resolved is T typed))
            {
                throw new ArgumentException(
                    $"'{argumentName}' resolved to {resolved.GetType().Name}, not {typeof(T).Name}.");
            }

            return typed;
        }
    }
}
