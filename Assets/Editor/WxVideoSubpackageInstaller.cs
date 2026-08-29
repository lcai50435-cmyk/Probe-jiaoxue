using System.IO;
using M1;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace M1.EditorTools
{
    /// <summary>
    /// 微信导出后处理：把引导视频放入 minigame/videos 普通分包（单个普通分包不限大小，主包 4M 不受影响），
    /// 补 game.json 分包与 parallelPreloadSubpackages 声明（与 wasmcode/data 并行预载，玩家无感知），
    /// 并在 game.js 末尾注入启动加载信号——结果写 storage（M1IntroVideo.PackageReadyKey），由 M1IntroVideo 轮询。
    /// 幂等：重复导出安全；视频文件缺失时整体跳过（运行时自动回退 CDN），不阻塞导出。
    /// </summary>
    public static class WxVideoSubpackageInstaller
    {
        /// <summary>包内引导视频源文件（项目根目录相对路径，不进 Assets，避免被打进 data 包或引入构建）。</summary>
        public const string SourceRelative = "WeChatVideos/m1-intro-wx.mp4";
        public const string SubpackageName = "videos";
        private const string Marker = "[WxVideoSubpackageInstaller]";

        public static void Install()
        {
            var dstRoot = Path.Combine(Application.dataPath, "..", "Builds", "WXExport", "minigame");
            if (!Directory.Exists(dstRoot))
            {
                Debug.LogWarning(Marker + " minigame 输出目录不存在，跳过分包注入：" + Path.GetFullPath(dstRoot));
                return;
            }
            var src = Path.Combine(Application.dataPath, "..", SourceRelative);
            if (!File.Exists(src))
            {
                Debug.LogError(Marker + " 包内引导视频缺失：" + Path.GetFullPath(src) + "（本次导出不含分包，运行时回退 CDN）");
                return;
            }

            var dstDir = Path.Combine(dstRoot, SubpackageName);
            Directory.CreateDirectory(dstDir);
            var dstFile = Path.Combine(dstDir, Path.GetFileName(src));
            File.Copy(src, dstFile, true);
            var kb = new FileInfo(dstFile).Length / 1024;
            Debug.Log(Marker + " 已复制引导视频进分包：" + Path.GetFullPath(dstFile) + "（" + kb + " KB）");

            var jsonOk = PatchGameJson(Path.Combine(dstRoot, "game.json"));
            if (jsonOk) PatchGameJs(Path.Combine(dstRoot, "game.js"));
        }

        /// <summary>向 game.json 的 subpackages 与 parallelPreloadSubpackages 追加 videos 分包；已存在则跳过。</summary>
        private static bool PatchGameJson(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError(Marker + " game.json 不存在，跳过分包声明。");
                return false;
            }
            var text = File.ReadAllText(path);
            if (!text.Contains("\"" + SubpackageName + "\""))
            {
                var spIdx = text.IndexOf("\"subpackages\"");
                if (spIdx < 0)
                {
                    Debug.LogError(Marker + " game.json 未找到 subpackages 字段，跳过分包声明。");
                    return false;
                }
                var close = text.IndexOf(']', spIdx); // subpackages 内无数组嵌套，取第一个 ']' 即数组尾
                text = text.Insert(close,
                    ",\n    {\n      \"name\" : \"" + SubpackageName + "\",\n      \"root\" : \"" + SubpackageName + "/\"\n    }");
            }
            var ppIdx = text.IndexOf("\"parallelPreloadSubpackages\"");
            if (ppIdx >= 0)
            {
                var ppClose = text.IndexOf(']', ppIdx);
                if (!text.Substring(ppIdx, ppClose - ppIdx).Contains("\"" + SubpackageName + "\""))
                    text = text.Insert(ppClose, ",\n    {\"name\" : \"" + SubpackageName + "\"}");
            }
            else
            {
                // 旧版插件未生成并行预载字段：紧跟 subpackages 数组之后补一个（数组尾 ']' 已在上方定位逻辑中更新过位置，重新定位）
                var spIdx2 = text.IndexOf("\"subpackages\"");
                var close2 = text.IndexOf(']', spIdx2);
                text = text.Insert(close2 + 1,
                    ",\n  \"parallelPreloadSubpackages\" : [\n    {\"name\" : \"" + SubpackageName + "\"}\n  ]");
            }
            File.WriteAllText(path, text);
            Debug.Log(Marker + " 已写入 videos 分包与并行预载声明：" + path);
            return true;
        }

        /// <summary>在 game.js 末尾注入启动加载信号：置 0 → loadSubpackage → 成功置 1 / 失败置 2；已注入则跳过。
        /// 必须走 GameGlobal.WXWASMSDK 的存储通道写——SDK 的读接口带进程内缓存，原生 wx.setStorageSync 直写会导致
        /// C# 轮询永远读到旧缓存值（key 一旦被缓存便不再回落到 wx.getStorageSync）。</summary>
        private static void PatchGameJs(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError(Marker + " game.js 不存在，跳过加载信号注入（运行时将走 CDN 兜底）。");
                return;
            }
            var js = File.ReadAllText(path);
            if (js.Contains(Marker)) return;
            var key = M1IntroVideo.PackageReadyKey;
            js += "\n" +
                  "//" + Marker + " 引导视频分包：启动即加载，经 SDK 存储通道写就绪信号供 Unity 轮询（M1IntroVideo.PackageReadyKey）\n" +
                  ";(function(){try{var K='" + key + "';" +
                  "var W=(typeof GameGlobal!=='undefined'&&GameGlobal.WXWASMSDK)?GameGlobal.WXWASMSDK:null;" +
                  "var set=function(v){if(W&&W.WXStorageSetStringSync){W.WXStorageSetStringSync(K,v);}else{wx.setStorageSync(K,v);}};" +
                  "set('0');" +
                  "wx.loadSubpackage({name:'" + SubpackageName + "'," +
                  "success:function(){set('1');}," +
                  "fail:function(e){set('2');console.warn('" + Marker + " videos 分包加载失败',e);}});" +
                  "}catch(e){try{wx.setStorageSync('" + key + "','2');}catch(_){}}})();\n";
            File.WriteAllText(path, js);
            Debug.Log(Marker + " 已注入分包加载信号到 game.js。");
        }
    }
}
