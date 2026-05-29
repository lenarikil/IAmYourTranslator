using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IAmYourTranslator.Utils
{
    public static class CachingHelpers
    {
        private static Dictionary<string, GameObject> rootObjectCache = new Dictionary<string, GameObject>();
        private static DateTime lastRootCacheTime = DateTime.MinValue;
        private static readonly TimeSpan ROOT_CACHE_DURATION = TimeSpan.FromSeconds(1f);

        private static Dictionary<(GameObject, string), GameObject> childCache = new Dictionary<(GameObject, string), GameObject>();
        private static DateTime lastChildCacheTime = DateTime.MinValue;
        private static readonly TimeSpan CHILD_CACHE_DURATION = TimeSpan.FromSeconds(0.5f);

        private static readonly Dictionary<Type, UnityEngine.Object[]> findObjectsCache = new Dictionary<Type, UnityEngine.Object[]>();
        private static DateTime findObjectsCacheTime = DateTime.MinValue;
        private static readonly TimeSpan FIND_OBJECTS_CACHE_DURATION = TimeSpan.FromMilliseconds(500);

        public static T[] FindObjectsOfTypeCached<T>(bool includeInactive = false) where T : UnityEngine.Object
        {
            var type = typeof(T);
            var now = DateTime.UtcNow;

            if ((now - findObjectsCacheTime) < FIND_OBJECTS_CACHE_DURATION && findObjectsCache.TryGetValue(type, out var cached))
            {
                if (cached != null)
                    return cached as T[];
            }

            var result = UnityEngine.Object.FindObjectsOfType<T>(includeInactive);
            findObjectsCache[type] = result;
            findObjectsCacheTime = now;
            return result;
        }

        public static void InvalidateFindObjectsCache()
        {
            findObjectsCache.Clear();
            findObjectsCacheTime = DateTime.MinValue;
        }

        public static void ClearAllCaches()
        {
            rootObjectCache.Clear();
            lastRootCacheTime = DateTime.MinValue;
            childCache.Clear();
            lastChildCacheTime = DateTime.MinValue;
            InvalidateFindObjectsCache();
        }

        public static GameObject GetInactiveRootObject(string objectName)
        {
            var now = DateTime.UtcNow;

            if ((now - lastRootCacheTime) < ROOT_CACHE_DURATION && rootObjectCache.TryGetValue(objectName, out GameObject cached))
            {
                if (cached != null)
                    return cached;
                else
                    rootObjectCache.Remove(objectName);
            }

            if ((now - lastRootCacheTime) >= ROOT_CACHE_DURATION)
            {
                rootObjectCache.Clear();
                lastRootCacheTime = now;
            }

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root.name == objectName)
                {
                    rootObjectCache[objectName] = root;
                    return root;
                }
            }

            rootObjectCache[objectName] = null;
            return null;
        }

        public static GameObject GetGameObjectChild(GameObject parent, string childName)
        {
            if (parent == null) return null;

            var cacheKey = (parent, childName);
            var now = DateTime.UtcNow;

            if ((now - lastChildCacheTime) < CHILD_CACHE_DURATION && childCache.TryGetValue(cacheKey, out GameObject cached))
            {
                if (cached != null)
                    return cached;
                else
                    childCache.Remove(cacheKey);
            }

            if ((now - lastChildCacheTime) >= CHILD_CACHE_DURATION)
            {
                childCache.Clear();
                lastChildCacheTime = now;
            }

            Transform child = parent.transform.Find(childName);
            if (child != null)
            {
                childCache[cacheKey] = child.gameObject;
                return child.gameObject;
            }

            childCache[cacheKey] = null;
            return null;
        }

        public static IEnumerator WaitforSeconds(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }
    }
}
