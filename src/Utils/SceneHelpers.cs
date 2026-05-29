using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IAmYourTranslator.Utils
{
    public static class SceneHelpers
    {
        public static string GetCurrentSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }

        public static Scene GetCurrentScene()
        {
            return SceneManager.GetActiveScene();
        }

        public static GameObject GetObject(string path)
        {
            string rootPath, restPath = null;

            if (!path.Contains('/'))
                rootPath = path;
            else
            {
                var pathParts = path.Split(new[] { '/' }, 2);
                rootPath = pathParts[0];
                restPath = pathParts[1];
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Logging.Warn($"[SceneHelpers] GetObject: Scene is not valid, returning null for '{path}'");
                return null;
            }

            GameObject[] roots;
            try
            {
                roots = activeScene.GetRootGameObjects();
            }
            catch (Exception e)
            {
                Logging.Warn($"[SceneHelpers] GetObject: Failed to get root objects: {e.Message}");
                return null;
            }

            foreach (var root in roots)
            {
                if (root.name != rootPath)
                    continue;

                if (restPath == null)
                    return root;

                var result = FindChildRecursive(root.transform, restPath);
                if (result != null)
                    return result.gameObject;
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string path)
        {
            var split = path.Split('/');
            return FindRecursiveInternal(parent, split, 0);
        }

        private static Transform FindRecursiveInternal(Transform current, string[] split, int index)
        {
            if (current == null || index >= split.Length)
                return current;

            var child = current.Find(split[index]);
            if (child == null)
            {
                foreach (Transform t in current)
                {
                    if (t.name == split[index])
                    {
                        child = t;
                        break;
                    }
                }
            }

            return FindRecursiveInternal(child, split, index + 1);
        }

        public static Transform RecursiveFindChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;

                Transform result = RecursiveFindChild(child, childName);
                if (result != null)
                    return result;
            }
            return null;
        }

        public static IEnumerable<CodeInstruction> IL(params (OpCode, object)[] instructions)
        {
            return instructions.Select(i => new CodeInstruction(i.Item1, i.Item2)).ToList();
        }
    }
}
