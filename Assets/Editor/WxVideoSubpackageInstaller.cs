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
    /// 并在 game.js 末尾注入启动加载：分包 MP4 复制到微信用户目录后，把就绪信号与真实路径写 storage 供 Unity 轮询。
    /// 幂等：重复导出安全；视频文件缺失时整体跳过（运行时自动回退 CDN），不阻塞导出。
    /// </summary>
    public static class WxVideoSubpackageInstaller
    {
        /// <summary>包内引导视频源文件（项目根目录相对路径，不进 Assets，避免被打进 data 包或引入构建）。</summary>
        public const string SourceRelative = "WeChatVideos/m1-intro-wx.mp4";
        public const string SubpackageName = "videos";
        private const string Marker = "[WxVideoSubpackageInstaller]";
        private const string EntryFileName = "game.js";

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
            if (js.Contains(Marker)) return;
            var key = M1IntroVideo.PackageReadyKey;
            var pathKey = M1IntroVideo.PackagePathKey;
            var fileName = Path.GetFileName(SourceRelative);
            var packagePath = SubpackageName + "/" + fileName;
            js += "\n" +
                  "//" + Marker + " 引导视频分包：复制到 USER_DATA_PATH 后写就绪信号与真实路径供 Unity 轮询\n" +
                  ";(function(){try{var K='" + key + "',P='" + pathKey + "',S='" + packagePath + "';" +
                  "var W=(typeof GameGlobal!=='undefined'&&GameGlobal.WXWASMSDK)?GameGlobal.WXWASMSDK:null;" +
                  "var set=function(k,v){if(W&&W.WXStorageSetStringSync){W.WXStorageSetStringSync(k,v);}else{wx.setStorageSync(k,v);}};" +
                  "var D=wx.getDeviceInfo?wx.getDeviceInfo():wx.getSystemInfoSync(),A=D.platform==='android';" +
                  "set(K,'0');set(P,'');" +
                  "wx.loadSubpackage({name:'" + SubpackageName + "',success:function(){if(!A){set(P,S);set(K,'1');return;}var d=wx.env.USER_DATA_PATH+'/" + fileName + "';" +
                  "try{var f=wx.getFileSystemManager();try{f.unlinkSync(d);}catch(_){}f.copyFileSync(S,d);set(P,d);set(K,'1');}" +
                  "catch(e){set(P,S);set(K,'1');console.warn('" + Marker + " Android 分包视频复制失败，尝试相对路径',e);}}," +
                  "fail:function(e){set(K,'2');console.warn('" + Marker + " videos 分包加载失败',e);}});" +
                  "}catch(e){try{wx.setStorageSync('" + key + "','2');}catch(_){}}})();\n";
            File.WriteAllText(path, js);
            Debug.Log(Marker + " 已注入分包加载信号到 game.js。");
        }
    }
}
