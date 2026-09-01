using System;
using System.IO;
using M1;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace M1.EditorTools
{
    /// <summary>
    /// 微信导出后处理：把引导视频放入 minigame/videos 普通分包，并生成微信要求的分包 game.js 入口，
    /// 补 game.json 分包与 parallelPreloadSubpackages 声明（与 wasmcode/data 并行预载，玩家无感知），
    /// 并在 game.js 启动 Unity 前注入加载：分包 MP4 复制到微信用户目录后，把就绪信号与真实路径写 storage 供 Unity 轮询。
    /// 幂等：重复导出安全；视频文件缺失时整体跳过（运行时自动回退 CDN），不阻塞导出。
    /// </summary>
    public static class WxVideoSubpackageInstaller
    {
        /// <summary>包内引导视频源文件（项目根目录相对路径，不进 Assets，避免被打进 data 包或引入构建）。</summary>
        public const string SourceRelative = "WeChatVideos/m1-intro-wx.mp4";
        public const string SubpackageName = "videos";
        private const string Marker = "[WxVideoSubpackageInstaller]";
        private const string EntryFileName = "game.js";
        private const string GameStartAnchor = "gameManager.startGame();";
        private const string InjectionHeader = "//[WxVideoSubpackageInstaller] 引导视频分包：复制到 USER_DATA_PATH 后写就绪信号与真实路径供 Unity 轮询";

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
            var entryFile = Path.Combine(dstDir, EntryFileName);
            if (!File.Exists(entryFile))
                File.WriteAllText(entryFile, "// videos subpackage entry.\n");
            var dstFile = Path.Combine(dstDir, Path.GetFileName(src));
            File.Copy(src, dstFile, true);
            var kb = new FileInfo(dstFile).Length / 1024;
            Debug.Log(Marker + " 已复制引导视频进分包：" + Path.GetFullPath(dstFile) + "（" + kb + " KB）");

            var jsonOk = PatchGameJson(Path.Combine(dstRoot, "game.json"));
            if (jsonOk)
            {
                PatchGameJs(Path.Combine(dstRoot, "game.js"));
                PatchVideoDecoderCache(dstRoot);
            }
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

        /// <summary>注入启动加载：Android 复制 MP4 到 USER_DATA_PATH；iOS/开发者工具保留已验证的分包相对路径。
        /// 两平台视频后端不同（Android VideoDecoder / iOS WKVideo），禁止强行共用 wxfile 路径。
        /// 必须走 GameGlobal.WXWASMSDK 存储通道，确保 C# 能读到进程内缓存的新值。</summary>
        private static void PatchGameJs(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError(Marker + " game.js 不存在，跳过加载信号注入（运行时将走 CDN 兜底）。");
                return;
            }
            var js = File.ReadAllText(path);
            if (CountOccurrences(js, GameStartAnchor) != 1)
            {
                Debug.LogError(Marker + " game.js 未找到唯一的 gameManager.startGame() 锚点，拒绝写入分包加载信号。");
                return;
            }
            var startIndex = js.IndexOf(GameStartAnchor, StringComparison.Ordinal);
            if (js.LastIndexOf("checkVersion().then", startIndex, StringComparison.Ordinal) < 0)
            {
                Debug.LogError(Marker + " gameManager.startGame() 不在 checkVersion().then 作用域内，拒绝写入分包加载信号。");
                return;
            }
            if (!RemovePreviousInjection(ref js)) return;

            var key = M1IntroVideo.PackageReadyKey;
            var pathKey = M1IntroVideo.PackagePathKey;
            var fileName = Path.GetFileName(SourceRelative);
            var packagePath = SubpackageName + "/" + fileName;
            var injection = InjectionHeader + "\n" +
                  ";(function(){try{var K='" + key + "',P='" + pathKey + "',Q='" + VideoDeliveryConfig.WechatPlatformKey + "',S='" + packagePath + "';" +
                  "var W=(typeof GameGlobal!=='undefined'&&GameGlobal.WXWASMSDK)?GameGlobal.WXWASMSDK:null;" +
                  "var set=function(k,v){if(W&&W.WXStorageSetStringSync){W.WXStorageSetStringSync(k,v);}else{wx.setStorageSync(k,v);}};" +
                  "var D=wx.getDeviceInfo?wx.getDeviceInfo():wx.getSystemInfoSync(),R=D.platform==='android'?'android':D.platform==='ios'?'ios':'unknown',A=R==='android';" +
                  "set(Q,R);if(!A){set(K,'2');return;}set(K,'0');set(P,'');" +
                  "wx.loadSubpackage({name:'" + SubpackageName + "',success:function(){var d=wx.env.USER_DATA_PATH+'/" + fileName + "';" +
                  "try{var f=wx.getFileSystemManager();try{f.unlinkSync(d);}catch(_){}f.copyFileSync(S,d);set(P,d);set(K,'1');}" +
                  "catch(e){set(P,S);set(K,'1');console.warn('" + Marker + " Android 分包视频复制失败，尝试相对路径',e);}}," +
                  "fail:function(e){set(K,'2');console.warn('" + Marker + " videos 分包加载失败',e);}});" +
                  "}catch(e){try{wx.setStorageSync('" + key + "','2');}catch(_){}}})();\n";
            startIndex = js.IndexOf(GameStartAnchor, StringComparison.Ordinal);
            js = js.Insert(startIndex, injection);
            File.WriteAllText(path, js);
            Debug.Log(Marker + " 已在 gameManager.startGame() 前注入分包加载信号。");
        }

        private static bool RemovePreviousInjection(ref string js)
        {
            var start = js.IndexOf(InjectionHeader, StringComparison.Ordinal);
            if (start < 0) return true;
            if (js.IndexOf(InjectionHeader, start + InjectionHeader.Length, StringComparison.Ordinal) >= 0)
            {
                Debug.LogError(Marker + " game.js 包含多个旧注入块，拒绝重写。");
                return false;
            }
            var end = js.IndexOf("})();", start, StringComparison.Ordinal);
            if (end < 0)
            {
                Debug.LogError(Marker + " game.js 旧注入块不完整，拒绝重写。");
                return false;
            }
            end += 4;
            if (end < js.Length && js[end] == '\r') end++;
            if (end < js.Length && js[end] == '\n') end++;
            js = js.Remove(start, end - start);
            return true;
        }

        private static void PatchVideoDecoderCache(string dstRoot)
        {
            var path = Path.Combine(dstRoot, "unity-sdk", "video", "index.js");
            if (!File.Exists(path))
            {
                Debug.LogWarning(Marker + " VideoDecoder 文件不存在，跳过缓存开关：" + path);
                return;
            }
            const string enabled = "const needCache = true;";
            const string disabled = "const needCache = false;";
            var text = File.ReadAllText(path);
            if (text.Contains(disabled)) return;
            if (CountOccurrences(text, enabled) != 1)
            {
                Debug.LogWarning(Marker + " VideoDecoder 未找到唯一的 needCache 常量，跳过缓存开关：" + path);
                return;
            }
            File.WriteAllText(path, text.Replace(enabled, disabled));
            Debug.Log(Marker + " 已关闭 VideoDecoder 跨场景缓存：" + path);
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }
    }
}
