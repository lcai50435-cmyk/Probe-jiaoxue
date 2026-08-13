using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace M2.EditorTools
{
    /// <summary>M2 最终冻结只读验收；一次性收口写入能力已永久移除。</summary>
    public static class M2FinalCloseout
    {
        private const string ScenePath = "Assets/Settings/Scenes/M2.unity";
        private const string FrozenM2Hash = "3ef75ced51304258b5bde9b43be8f354b247753801a708ae52b922b5829c990b";

        [MenuItem("Tools/M2/Verify Final Closeout（只读验收）")]
        public static void Run() => Verify(false);

        public static void M2FinalCloseoutBatch() => Verify(true);

        private static void Verify(bool batch)
        {
            var current = Sha256(ScenePath);
            if (current != FrozenM2Hash)
            {
                Debug.LogError($"[M2FinalCloseout] M2 冻结哈希不匹配：{current}，期望：{FrozenM2Hash}。只读入口不会修改 Scene。");
                if (batch) EditorApplication.Exit(1);
                return;
            }
            Debug.Log("[M2FinalCloseout] M2 已完成并冻结，SHA-256=" + current);
            if (batch) EditorApplication.Exit(0);
        }

        private static string Sha256(string path)
        {
            try
            {
                using var sha = SHA256.Create();
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception e)
            {
                Debug.LogError("[M2FinalCloseout] 读取 Scene 失败：" + e.Message);
                return "<unavailable>";
            }
        }
    }
}
