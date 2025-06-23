using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Controllers;
using UnityEngine;

public class AnimationCommandReceiver : MonoBehaviour
{
    private Controller _owner;
    private static readonly Regex methodCallRegex = new(@"^(Component|System)\.(\w+)\s*\((.*)\)$", RegexOptions.Compiled);
    private void Start()
    {
        _owner = GetComponent<Controller>();
    }
    
    public void InvokeByAnimationEvent(string command)
    {
        var match = methodCallRegex.Match(command);
        if (!match.Success)
        {
            Debug.LogWarning($"Invalid animation command format: {command}");
            return;
        }

        string targetType = match.Groups[1].Value;
        string methodName = match.Groups[2].Value;
        string argsRaw = match.Groups[3].Value;

        object target = null;
        Dictionary<Type, object> sourceDict = targetType == "Component"
            ? _owner.Components.ToDictionary(k => k.Key, v => (object)v.Value)
            : _owner.Systems.ToDictionary(k => k.Key, v => (object)v.Value);

        foreach (var pair in sourceDict)
        {
            var method = pair.Key.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                object[] parameters = ParseParameters(method, argsRaw);
                method.Invoke(pair.Value, parameters);
                return;
            }
        }

        Debug.LogWarning($"Method {methodName} not found in {targetType}s");
    }

    private object[] ParseParameters(MethodInfo method, string argsRaw)
    {
        var paramInfos = method.GetParameters();
        if (string.IsNullOrWhiteSpace(argsRaw) && paramInfos.Length == 0)
            return Array.Empty<object>();

        string[] args = SplitArguments(argsRaw);
        if (args.Length != paramInfos.Length)
        {
            Debug.LogWarning($"Parameter count mismatch for {method.Name}. Expected {paramInfos.Length}, got {args.Length}");
            return Array.Empty<object>();
        }

        object[] finalArgs = new object[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            finalArgs[i] = ConvertArg(args[i], paramInfos[i].ParameterType);
        }
        return finalArgs;
    }

    private string[] SplitArguments(string input)
    {
        List<string> result = new List<string>();
        int depth = 0;
        int lastSplit = 0;
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '(') depth++;
            else if (input[i] == ')') depth--;
            else if (input[i] == ',' && depth == 0)
            {
                result.Add(input.Substring(lastSplit, i - lastSplit).Trim());
                lastSplit = i + 1;
            }
        }
        result.Add(input.Substring(lastSplit).Trim());
        return result.ToArray();
    }

    private object ConvertArg(string input, Type targetType)
    {
        input = input.Trim();

        if (targetType == typeof(int))
            return int.Parse(input);
        if (targetType == typeof(float))
            return float.Parse(input);
        if (targetType == typeof(bool))
            return bool.Parse(input);
        if (targetType == typeof(string))
            return input.Trim('"');

        if (targetType == typeof(Vector3))
        {
            Match m = Regex.Match(input, @"Vector3\s*\(\s*([-\d.]+)\s*,\s*([-\d.]+)\s*,\s*([-\d.]+)\s*\)");
            if (m.Success)
                return new Vector3(float.Parse(m.Groups[1].Value), float.Parse(m.Groups[2].Value), float.Parse(m.Groups[3].Value));
        }

        if (targetType == typeof(Vector2))
        {
            Match m = Regex.Match(input, @"Vector2\s*\(\s*([-\d.]+)\s*,\s*([-\d.]+)\s*\)");
            if (m.Success)
                return new Vector2(float.Parse(m.Groups[1].Value), float.Parse(m.Groups[2].Value));
        }

        Debug.LogWarning($"Unsupported parameter type or format: '{input}' to {targetType.Name}");
        return null;
    }
}
