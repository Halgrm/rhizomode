#nullable enable

using UnityEngine;

using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Model
{
    /// <summary>
    /// メニューに表示する3Dオブジェクトプレハブのリスト。
    /// Inspectorでプレハブを追加するだけで新しいObject3Dノードが使える。
    /// </summary>
    [CreateAssetMenu(fileName = "Object3DPrefabList", menuName = "Rhizomode/Object3D Prefab List")]
    public class Object3DPrefabList : ScriptableObject
    {
        [SerializeField] private GameObject[] prefabs = System.Array.Empty<GameObject>();

        /// <summary>登録されたプレハブ一覧。</summary>
        public GameObject[] Prefabs => prefabs;
    }
}
