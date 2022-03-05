using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using DevConsole.Commands;
using UnityEngine;
using Object = UnityEngine.Object;
using static DevConsole.GameConsole;

namespace DevConsole
{
    internal static partial class BuiltInCommands
    {
        private static void TryRegisterBepInExCommands()
        {
            try
            {
                RegisterBepInExCommands();
            }
            catch (Exception e) when (e is DllNotFoundException or TypeLoadException)
            {
                Debug.Log("Failed to load BepInEx, assuming not available.");
            }
        }

        private static void RegisterBepInExCommands()
        {
            // Will immediately throw if BepInEx not available.
            Utility.IsNullOrWhiteSpace("");

            new CommandBuilder("bepcfg")
                .Run(args =>
                {
                    if (args.Length is not (3 or 4))
                    {
                        WriteLine("Expected 3 or 4 arguments");
                        return;
                    }

                    var pluginGuid = args[0];
                    var entrySection = args[1];
                    var entryName = args[2];

                    var config = Object.FindObjectsOfType<BaseUnityPlugin>()
                        .SingleOrDefault(p => p.Info.Metadata.GUID == pluginGuid)?.Config;

                    if (config == null)
                    {
                        WriteLine($"Unable to find plugin '{pluginGuid}'");
                        return;
                    }

                    var def = new ConfigDefinition(entrySection, entryName);

                    if (!config.ContainsKey(def))
                    {
                        WriteLine($"Unable to find config entry '{entryName}' under '{entrySection}'");
                        return;
                    }

                    var entry = config[def];

                    if (args.Length == 4)
                    {
                        // Set value
                        try
                        {
                            entry.BoxedValue = TomlTypeConverter.ConvertToValue(args[3], entry.SettingType);
                        }
                        catch (Exception e)
                        {
                            WriteLine($"Error setting value, make sure it is correct");
                            Debug.LogWarning($"bepcfg command error when setting {entrySection}.{entryName}: {e}");
                        }
                    }
                    else
                    {
                        // Get value
                        WriteLine(entry.GetSerializedValue());
                    }
                })
                .AutoComplete(args =>
                {
                    if (args.Length == 0)
                        return Object.FindObjectsOfType<BaseUnityPlugin>()
                            .Where(p => p.Config.Count > 0)
                            .Select(p => p.Info.Metadata.GUID);

                    var pluginGuid = args[0];

                    var config = Object.FindObjectsOfType<BaseUnityPlugin>()
                        .SingleOrDefault(p => p.Info.Metadata.GUID == pluginGuid)?.Config;

                    if (config == null)
                        return Enumerable.Empty<string>();

                    if (args.Length == 1)
                        return config.Keys.Select(d => d.Section).Distinct();

                    var entrySection = args[1];
                    if (args.Length == 2)
                        return config.Keys.Where(d => d.Section == entrySection).Select(d => d.Key);

                    var entryName = args[2];

                    var def = new ConfigDefinition(entrySection, entryName);

                    if (!config.ContainsKey(def))
                        return Enumerable.Empty<string>();

                    var entry = config[def];

                    return BepGetPossibleConfigValues(entry);
                })
                .Help("bepcfg [plugin GUID] [section] [key] [new value?]")
                .Register();
        }

        private static IEnumerable<string> BepGetPossibleConfigValues(object entryObj)
        {
            var entry = (ConfigEntryBase)entryObj;

            if (entry.SettingType.IsEnum)
                return Enum.GetNames(entry.SettingType);

            if (entry.SettingType == typeof(bool))
                return new[] { "True", "False" };

            if (BepTryAcceptableValueList(entry.Description.AcceptableValues, out var names))
                return names;

            return Enumerable.Empty<string>();
        }

        private static bool BepTryAcceptableValueList(
            object valueBaseObj,
            out IEnumerable<string> values)
        {
            var valueBase = (AcceptableValueBase)valueBaseObj;

            values = null;
            if (valueBase == null)
                return false;

            var type = valueBase.GetType();
            if (type.GetGenericTypeDefinition() != typeof(AcceptableValueList<>))
                return false;

            var valueType = type.GetGenericArguments()[0];

            // ReSharper disable once PossibleNullReferenceException
            var helper = typeof(BuiltInCommands)
                .GetMethod(nameof(BepAcceptableValueListHelper), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(valueType);

            values = (IEnumerable<string>)helper.Invoke(null, new object[] { valueBase });
            return true;
        }

        private static IEnumerable<string> BepAcceptableValueListHelper<T>(object listObj) where T : IEquatable<T>
        {
            var list = (AcceptableValueList<T>)listObj;

            foreach (var value in list.AcceptableValues)
            {
                yield return TomlTypeConverter.ConvertToString(value, typeof(T));
            }
        }
    }
}