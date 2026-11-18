using UnityEngine;

namespace TideSong
{
    /// <summary>场景里唯一需要挂的脚本：把整个游戏搭起来。</summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        void Awake()
        {
            if (FindObjectOfType<GameManager>() != null) return;
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
        }
    }
}
