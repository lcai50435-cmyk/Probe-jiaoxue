// ============================================================================
// 生成支持中文的 TextMeshPro 字体资产（Sarasa Gothic SC）
// ----------------------------------------------------------------------------
// 原因：Unity 的 TextMeshPro 不能直接把 .ttf 拖给 TMP Text 使用，
//       必须先用 TTF 生成 TMP Font Asset（SDF 图集）。
//       如果生成时只勾选了默认的 ASCII 字符集，中文就会显示成方框/乱码。
// 本脚本生成“Dynamic（动态）”字体资产，并预烘焙 GB2312 常用汉字 + 全角标点；
// 运行时遇到没预烘焙到的生僻字，TMP 也会自动补充字形，因此不会再出现乱码。
//
// 注意：
//  - 输出到 *_cn.asset 新文件，绝不覆盖原有 SDF 资产（避免 GUID 变化导致
//    场景中 TMP 的字体引用断裂）。场景字体重指向由 Tools/M1/Setup M1-1 完成。
//  - TMP_FontAsset.CreateFontAsset() 只创建内存中的 Material / 图集 Texture，
//    必须用 AssetDatabase.AddObjectToAsset() 把它们写进 .asset 文件，否则
//    序列化后 m_Material / m_AtlasTextures 全是 {fileID: 0}，中文仍会乱码。
//
// 用法：菜单 Tools > 字体 > 重新生成中文字体资产 (Sarasa Gothic)
// ============================================================================
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using UnityEngine;

public static class GenerateChineseFont
{
    private const string FontFolder = "Assets/font/sarasa-gothic-sc-regular";

    // SDF 采样点大小：数值越大字越清晰，但图集越大、文件越大。
    // 60 适合 UI 文字（约 12~120px 显示大小），如需更大字号可改成 90/132 后重新生成。
    private const int SamplingPointSize = 60;
    private const int AtlasPadding = 8;
    private const int AtlasSize = 4096;

    [MenuItem("Tools/字体/重新生成中文字体资产 (Sarasa Gothic) %#&g")]
    public static void GenerateAllFromMenu()
    {
        GenerateAll();
    }

    public static void GenerateAll()
    {
        // 输出到新路径 *_cn.asset，避免覆盖旧资产导致 GUID/引用变化。
        Generate("sarasa-gothic-sc-regular.ttf", "sarasa-gothic-sc-regular_cn.asset");
        Generate("sarasa-gothic-sc-light.ttf", "sarasa-gothic-sc-light_cn.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[字体] 中文字体资产生成完成！请在 Tools/M1/Setup M1-1 中把场景 TMP 字体重指向到 *_cn.asset。");
    }

    private static void Generate(string ttfFileName, string assetFileName)
    {
        string ttfPath = Path.Combine(FontFolder, ttfFileName).Replace('\\', '/');
        string outPath = Path.Combine(FontFolder, assetFileName).Replace('\\', '/');

        Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            Debug.LogError("[字体] 无法加载字体文件：" + ttfPath);
            return;
        }

        // 必须把字体数据打进包，动态 SDF 运行时才能用 FontEngine 补充字形
        var importer = AssetImporter.GetAtPath(ttfPath) as TrueTypeFontImporter;
        if (importer != null && !importer.includeFontData)
        {
            importer.includeFontData = true;
            importer.SaveAndReimport();
            font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        }

        // 创建 Dynamic 模式的 TMP 字体资产（图集 4096x4096，多图集支持）
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            font,
            SamplingPointSize,
            AtlasPadding,
            GlyphRenderMode.SDFAA,
            AtlasSize,
            AtlasSize,
            AtlasPopulationMode.Dynamic,
            true);

        if (fontAsset == null)
        {
            Debug.LogError("[字体] TMP 字体资产生成失败：" + ttfPath);
            return;
        }

        // 如果 *_cn.asset 已存在（重复运行），先删除再创建；此文件没有场景引用，
        // GUID 变化不影响场景（场景重指向按路径加载）。
        if (File.Exists(outPath))
        {
            AssetDatabase.DeleteAsset(outPath);
        }

        // 先落盘主资产，再把 Material 和全部图集 Texture 作为子资产写进同一个 .asset 文件。
        AssetDatabase.CreateAsset(fontAsset, outPath);
        var addedSubAssets = new HashSet<UnityEngine.Object>();
        AddSubAssets(fontAsset, addedSubAssets);

        // 预烘焙：ASCII + GB2312 一级/二级汉字（约 6763 字）+ 常用全角标点
        string characters = BuildCharacterSet();
        fontAsset.TryAddCharacters(characters, out string missing);
        if (!string.IsNullOrEmpty(missing))
        {
            Debug.LogWarning("[字体] " + ttfFileName + " 有 " + missing.Length + " 个字符未预烘焙（Dynamic 模式运行时会自动补充）：" + missing);
        }

        // 多图集模式下 TryAddCharacters 可能新增了第 2/3/4 张图集，必须一并写进资产。
        AddSubAssets(fontAsset, addedSubAssets);

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();

        Debug.Log("[字体] 已生成：" + outPath + "（采样点 " + SamplingPointSize + "px，图集 " + AtlasSize + "x" + AtlasSize + "，Dynamic 模式，" +
                  "图集数量 " + fontAsset.atlasTextures.Length + "，材质 " + (fontAsset.material != null ? "有" : "无") + "）");
    }

    private static void AddSubAssets(TMP_FontAsset fontAsset, HashSet<UnityEngine.Object> addedSubAssets)
    {
        // TMP 在字资产已持久化时（CreateAsset 之后）会自动把新增的图集纹理加入 .asset（见
        // TMP_EditorResourceManager.AddTextureToAsset）。这里先判断是否已是子资产，避免
        // AddObjectToAsset 因“对象已是资产”而抛异常。
        if (fontAsset.material != null && addedSubAssets.Add(fontAsset.material))
        {
            if (!AssetDatabase.IsSubAsset(fontAsset.material))
            {
                try { AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset); }
                catch (System.Exception e) { Debug.LogError("[字体] 添加材质子资产失败：" + e.Message); }
            }
        }
        if (fontAsset.atlasTextures != null)
        {
            foreach (var tex in fontAsset.atlasTextures)
            {
                if (tex == null || !addedSubAssets.Add(tex)) continue;
                if (AssetDatabase.IsSubAsset(tex)) continue; // TMP 已自动加入
                try { AssetDatabase.AddObjectToAsset(tex, fontAsset); }
                catch (System.Exception e) { Debug.LogError("[字体] 添加图集子资产失败：" + e.Message); }
            }
        }
    }

    private static string BuildCharacterSet()
    {
        var set = new HashSet<char>();

        // ASCII 可打印字符
        for (int c = 32; c <= 126; c++) set.Add((char)c);

        // GB2312 汉字：一级（区16~55）+ 二级（区56~87），共约 6763 字
        Encoding gb = null;
        try { gb = Encoding.GetEncoding(936); } catch { gb = null; }

        if (gb != null)
        {
            for (int area = 16; area <= 87; area++)
            {
                for (int pos = 1; pos <= 94; pos++)
                {
                    byte[] bytes = { (byte)(area + 0xA0), (byte)(pos + 0xA0) };
                    string s = gb.GetString(bytes);
                    if (s.Length == 1)
                    {
                        char ch = s[0];
                        // 过滤无效区位解码出的替换字符
                        if (ch != '?' && ch != '\uFFFD') set.Add(ch);
                    }
                }
            }
        }
        else
        {
            // 兜底：如果系统缺少 GB2312 代码页，至少内置一批最常用字
            foreach (char c in "的一是在不了有和人这中大为上个国我以要他时来用们生到作地于出就分对成会可主发年动同工也能下过子说产种面而方后多定行学法所民得经十三之进着等部度家电力里如水化高自二理起小物现实加量都两体制机当使点从业本去把性好应开它合还因由其些然前外天政四日那社义事平形相全表间样与关各重新线内数正心反你明看原又么利比或但质气第向道命此变条只没结解问意建月公无系军很情者最立代想已通并提直题党程展五果料象员革位入常文总次品式活设及管特件长求老头基资边流路级少图山统接知较将组见计别她手角期根论运农指几九区强放决西被干做必战先回则任取据处队南给色光门即保治北造百规热领七海口东导器压志世金增争济阶油思术极交受联什认六共权收证改清己美再采转更单风切打白教速花带安场身车例真务具万每目至达走积示议声报斗完类八离华名确才科张信马节话米整空元况今集温传土许步群广石记需段研界拉林律叫且究观越织装影算低持音众书布复容儿须际商非验连断深难近矿千周委素技备半办青省列习响约支般史感劳便团往酸历市克何除消构府称太准精值号率族维划选标写存候毛亲快效斯院查江型眼王按格养易置派层片始却专状育厂京识适属圆包火住调满县局照参红细引听该铁价严龙飞")
                set.Add(c);
        }

        // 常用全角标点 / 符号
        foreach (char c in "\u3001\uFF0C\u3002\uFF01\uFF1F\uFF1B\uFF1A\u201C\u201D\u2018\u2019\uFF08\uFF09\u3010\u3011\u300A\u300B\u3008\u3009\u2014\u2026\u00B7\uFF5E\uFFE5\u00D7\u00F7\uFF0B\uFF0D\uFF1D\uFF1C\uFF1E\u00B1\u00B0\u2103\u300C\u300D\u300E\u300F\uFF04\uFF05\uFF06\uFF0A\uFF03\uFF20\uFF3E\uFF3F\uFF5C\uFF40\u2013\u2032\u2033\u3014\u3015\u3016\u3017\u3018\u3019")
            set.Add(c);

        var sb = new StringBuilder(set.Count);
        foreach (char c in set) sb.Append(c);
        return sb.ToString();
    }
}
#endif
// TouchMarker 20260805


