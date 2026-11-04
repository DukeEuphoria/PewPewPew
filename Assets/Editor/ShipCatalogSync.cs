using System;
using System.Collections.Generic;
using System.Linq;
using PewPewPew.Ships;
using PewPewPew.Weapons;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PewPewPew.Editor
{
    public class ShipCatalogSync : AssetPostprocessor, IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [MenuItem("PewPewPew/Loadout/Sync component catalog")]
        public static void Sync()
        {
            string[] catalogs = AssetDatabase.FindAssets("t:ShipCatalog", new[] { "Assets/Resources" });
            if (catalogs.Length != 1)
                throw new InvalidOperationException("Expected exactly one ShipCatalog in Assets/Resources.");

            var catalog = AssetDatabase.LoadAssetAtPath<ShipCatalog>(AssetDatabase.GUIDToAssetPath(catalogs[0]));
            var serialized = new SerializedObject(catalog);
            Append<HullDef>(serialized, "m_Hulls", "Hulls", 16);
            Append<ShieldDef>(serialized, "m_Shields", "Shields", 16);
            Append<GunDef>(serialized, "m_MainGuns", "MainWeapons", 16);
            Append<GunDef>(serialized, "m_SecondaryGuns", "SecondaryWeapons", 16);
            Append<ThrusterDef>(serialized, "m_Thrusters", "Thrusters", 16);
            Append<SubSystemDef>(serialized, "m_SubSystems", "SubSystems", 64);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static void Append<T>(SerializedObject catalog, string property, string folder, int capacity)
            where T : ShipComponentDef
        {
            SerializedProperty array = catalog.FindProperty(property);
            var existing = new HashSet<UnityEngine.Object>();
            for (int i = 0; i < array.arraySize; i++)
            {
                UnityEngine.Object entry = array.GetArrayElementAtIndex(i).objectReferenceValue;
                if (entry == null) throw new InvalidOperationException($"{property}[{i}] is missing. Restore the definition to preserve network indices.");
                if (!existing.Add(entry)) throw new InvalidOperationException($"{property} contains duplicate definition {entry.name}.");
            }

            string path = "Assets/ShipComponents/" + folder;
            if (!AssetDatabase.IsValidFolder(path)) throw new InvalidOperationException($"Missing component directory: {path}");
            var discovered = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { path })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<T>).Where(item => !existing.Contains(item)).ToArray();
            if (array.arraySize + discovered.Length > capacity)
                throw new InvalidOperationException($"{property} exceeds the supported {capacity} definitions.");

            // Existing indices are part of the network loadout; only append newly discovered definitions.
            foreach (T definition in discovered)
            {
                int index = array.arraySize++;
                array.GetArrayElementAtIndex(index).objectReferenceValue = definition;
            }
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Any(path => path.StartsWith("Assets/ShipComponents/", StringComparison.Ordinal) && path.EndsWith(".asset", StringComparison.Ordinal)))
                EditorApplication.delayCall += Sync;
        }

        public void OnPreprocessBuild(BuildReport report) => Sync();
    }
}
