using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gallop
{
    public class AssetHolder : MonoBehaviour
    {
        [SerializeField] public AssetTable _assetTable;
        [SerializeField] public AssetTableValue _assetTableValue;

        // 官方 AssetHolder 使用的单对象读取接口。
        public T Get<T>(string key) where T : Object
        {
            if (_assetTable == null ||
                _assetTable.list == null ||
                string.IsNullOrEmpty(key))
            {
                return null;
            }

            for (int i = 0; i < _assetTable.list.Count; i++)
            {
                StringObjectPair pair = _assetTable.list[i];
                if (pair == null || pair.Key != key)
                    continue;

                T value = ConvertObject<T>(pair.Value);
                if (value != null)
                    return value;
            }

            return null;
        }

        // 官方 LaserController 会调用 GetObjects<GameObject>("Ray"/"Light")。
        // AssetTable 允许多个条目使用同一个 Key；必须保持序列化顺序返回，
        // 因为 Ray 的 index 会直接参与 Formation 计算。
        public T[] GetObjects<T>(string key) where T : Object
        {
            if (_assetTable == null ||
                _assetTable.list == null ||
                string.IsNullOrEmpty(key))
            {
                return new T[0];
            }

            var result = new List<T>();

            for (int i = 0; i < _assetTable.list.Count; i++)
            {
                StringObjectPair pair = _assetTable.list[i];
                if (pair == null || pair.Key != key)
                    continue;

                T value = ConvertObject<T>(pair.Value);
                if (value != null)
                    result.Add(value);
            }

            return result.ToArray();
        }

        public float GetValue(string key)
        {
            if (_assetTableValue == null ||
                _assetTableValue.list == null ||
                string.IsNullOrEmpty(key))
            {
                return -1f;
            }

            for (int i = 0; i < _assetTableValue.list.Count; i++)
            {
                StringValuePair pair = _assetTableValue.list[i];
                if (pair != null && pair.Key == key)
                    return pair.Value;
            }

            return -1f;
        }

        private static T ConvertObject<T>(Object value) where T : Object
        {
            if (value == null)
                return null;

            if (value is T direct)
                return direct;

            if (typeof(T) == typeof(GameObject))
            {
                if (value is Component component)
                    return component.gameObject as T;
            }

            if (typeof(Component).IsAssignableFrom(typeof(T)))
            {
                if (value is GameObject gameObject)
                    return gameObject.GetComponent(typeof(T)) as T;

                if (value is Component component)
                    return component.GetComponent(typeof(T)) as T;
            }

            return null;
        }
    }
}
