#!/usr/bin/env python3
"""只读审计：解析 Unity Scene YAML，提取全部 UI 文本组件的字号与容器信息。

不依赖 Unity 运行；直接读场景序列化文件。输出 markdown 表格供人工审核。
"""
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]          # 项目根
SCENES = ROOT / "Assets" / "Settings" / "Scenes"
OUT = Path(__file__).resolve().parent / "scene-font-audit.md"

LINE_H_EM = 1.20      # 保守行高系数（Sarasa Gothic 约 1.0~1.17，取 1.2 留安全余量）
CJK_WIDTH_EM = 1.0    # CJK 全宽
ASCII_WIDTH_EM = 0.62 # 拉丁/数字保守平均宽（proportional）
PUNCT_WIDTH_EM = 0.40

OVF_WRAP, OVF_OVERFLOW, OVF_ELLIPSIS, OVF_TRUNCATE = 0, 1, 2, 4


_ESCAPE_RE = re.compile(r"\\u([0-9a-fA-F]{4})|\\x([0-9a-fA-F]{2})")


def _decode_escapes(s: str) -> str:
    s = s.replace("\\n", "\n").replace("\\t", "\t").replace('\\"', '"').replace("\\\\", "\\")
    return _ESCAPE_RE.sub(
        lambda m: chr(int(m.group(1) or m.group(2), 16)), s)


def parse_value(raw: str):
    raw = raw.strip()
    if raw == "{}" or raw == "":
        return {} if raw == "{}" else None
    if raw.startswith("{") and raw.endswith("}"):
        body = raw[1:-1].strip()
        if not body:
            return {}
        out = {}
        for part in _split_top(body):
            if ":" in part:
                k, v = part.split(":", 1)
                out[k.strip()] = parse_value(v)
        return out
    if raw.startswith("[") and raw.endswith("]"):
        body = raw[1:-1].strip()
        if not body:
            return []
        return [parse_value(p) for p in _split_top(body)]
    if raw in ("0", "1") or re.fullmatch(r"-?\d+", raw):
        return int(raw)
    if re.fullmatch(r"-?\d+\.\d+", raw):
        return float(raw)
    if raw.startswith('"') and raw.endswith('"'):
        return _decode_escapes(raw[1:-1])
    if raw.startswith("'") and raw.endswith("'"):
        return _decode_escapes(raw[1:-1])
    return _decode_escapes(raw)


def _split_top(body: str):
    parts, depth, cur = [], 0, ""
    for ch in body:
        if ch in "{[":
            depth += 1
        elif ch in "}]":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append(cur)
            cur = ""
        else:
            cur += ch
    if cur.strip():
        parts.append(cur)
    return parts


def parse_scene(path: Path):
    """返回 docs: list[(class_id, file_id, dict)]，dict 为嵌套字段。

    文档结构：`--- !u!114 &123` 下一行是根键（如 MonoBehaviour:），字段在其下，需剥掉根键层。
    """
    text = path.read_text(encoding="utf-8", errors="replace")
    docs = []
    header_re = re.compile(r"^--- !u!(\d+) &(\d+)")
    cur = None
    stack = []  # (indent, dict)
    for line in text.splitlines():
        m = header_re.match(line)
        if m:
            if cur:
                docs.append(cur)
            cur = {"__class__": int(m.group(1)), "__file__": int(m.group(2)), "__fields__": {}}
            stack = [(-1, cur["__fields__"])]
            continue
        if cur is None or not line.strip() or line.strip().startswith("#"):
            continue
        indent = len(line) - len(line.lstrip(" "))
        content = line.strip()
        while stack and indent <= stack[-1][0]:
            stack.pop()
        if not stack:
            continue
        target = stack[-1][1]
        if content.startswith("- "):
            item = content[2:]
            if not isinstance(target, list):
                continue
            target.append(parse_value(item))
            continue
        if ":" not in content:
            continue
        key, _, val = content.partition(":")
        key, val = key.strip(), val.strip()
        if val == "":
            child = {}
            target[key] = child
            stack.append((indent, child))
        else:
            target[key] = parse_value(val)
    if cur:
        docs.append(cur)
    for d in docs:
        vals = d["__fields__"]
        if len(vals) == 1 and isinstance(next(iter(vals.values())), dict):
            d["__fields__"] = next(iter(vals.values()))
    return docs


def char_width_em(ch: str) -> float:
    o = ord(ch)
    if o >= 0x2E80 or ch in "，。：；！？、（）“”‘’—…《》":
        return CJK_WIDTH_EM
    if ch == " ":
        return 0.30
    if ch in "iljt.,:;'!|":
        return 0.30
    if ch.isupper() or ch.isdigit():
        return 0.58
    return 0.50


def text_width_em(s: str) -> float:
    return sum(char_width_em(c) for c in s)


def wrap_lines(s: str, width_em_available: float) -> int:
    """按逐字断行（CJK 场景近似准确）估算换行后的行数。"""
    lines, cur = 1, 0.0
    for ch in s.split("\n") if "\n" in s else [s]:
        pass
    lines = 0
    for para in s.split("\n"):
        lines += 1
        cur = 0.0
        for ch in para:
            w = char_width_em(ch)
            if cur + w > width_em_available + 1e-6 and cur > 0:
                lines += 1
                cur = w
            else:
                cur += w
    return lines


def make_rect_size(rect_doc):
    """实际尺寸 = sizeDelta + (anchorMax-anchorMin) * parentSize；按 fileID 递归。"""

    def size_of(rect_fid, seen=None):
        seen = seen or set()
        if rect_fid in seen or rect_fid not in rect_doc:
            return (0.0, 0.0)
        seen.add(rect_fid)
        rt = rect_doc[rect_fid]
        f = rt.get("m_Father", {}).get("fileID") or None  # fileID:0 = 无父节点
        if f:
            pw, ph = size_of(f, seen)
        else:
            return (1920.0, 1080.0)  # 根（Canvas）：运行时由分辨率驱动 = 业务参考分辨率
        sd = rt.get("m_SizeDelta") or {}
        amin = rt.get("m_AnchorMin") or {}
        amax = rt.get("m_AnchorMax") or {}
        w = sd.get("x", 0) or 0
        h = sd.get("y", 0) or 0
        w += ((amax.get("x", 0) or 0) - (amin.get("x", 0) or 0)) * pw
        h += ((amax.get("y", 0) or 0) - (amin.get("y", 0) or 0)) * ph
        return (abs(w), abs(h))

    return size_of


def analyze_scene(path: Path):
    docs = parse_scene(path)
    by_file = {d["__file__"]: d for d in docs}
    go_name, go_rect, go_components = {}, {}, {}
    for d in docs:
        if d["__class__"] == 1:  # GameObject
            fid = d["__file__"]
            go_name[fid] = str(d["__fields__"].get("m_Name", "?"))
            go_components[fid] = [
                c.get("component", {}).get("fileID")
                for c in (d["__fields__"].get("m_Component") or [])
                if isinstance(c, dict)
            ]
        elif d["__class__"] in (4, 224):  # Transform / RectTransform
            go = d["__fields__"].get("m_GameObject", {}).get("fileID")
            if go:
                go_rect[go] = d["__file__"]

    rect_doc = {d["__file__"]: d["__fields__"] for d in docs if d["__class__"] in (4, 224)}

    def father_of(rect_fid):
        f = rect_doc.get(rect_fid, {}).get("m_Father", {}).get("fileID")
        if not f:
            return None
        g = rect_doc.get(f, {}).get("m_GameObject", {}).get("fileID")
        return f, g

    def chain(rect_fid):
        """从自身到根的 rect fileID 链。"""
        out = [rect_fid]
        cur = rect_fid
        seen = set()
        while True:
            nxt = father_of(cur)
            if not nxt or nxt[0] in seen:
                break
            seen.add(nxt[0])
            out.append(nxt[0])
            cur = nxt[0]
        return out

    size_of = make_rect_size(rect_doc)

    def path_of(rect_fid):
        names = []
        for rf in chain(rect_fid):
            g = rect_doc.get(rf, {}).get("m_GameObject", {}).get("fileID")
            names.append(go_name.get(g, "?") if g else "?")
        return "/".join(reversed(names))

    rows = []
    for d in docs:
        if d["__class__"] != 114:
            continue
        f = d["__fields__"]
        keys = f.keys()
        go = f.get("m_GameObject", {}).get("fileID")
        is_tmp = "m_fontSize" in keys and "m_text" in keys
        is_legacy = "m_FontData" in keys
        is_tmp_input = "m_TextComponent" in keys and "m_text" not in keys and "m_FontData" not in keys
        if not (is_tmp or is_legacy):
            continue
        rect_fid = go_rect.get(go)
        if rect_fid is None:
            continue
        size = size_of(rect_fid)
        common = {
            "scene": path.stem,
            "path": path_of(rect_fid),
            "name": go_name.get(go, "?"),
            "rect_w": round(size[0], 1),
            "rect_h": round(size[1], 1),
        }
        if is_tmp:
            fs = f.get("m_fontSize", 0)
            auto = bool(f.get("m_enableAutoSizing", 0))
            margin = f.get("m_margin") or {}
            mx = (margin.get("x", 0) or 0) + (margin.get("z", 0) or 0)
            my = (margin.get("y", 0) or 0) + (margin.get("w", 0) or 0)
            ovm = f.get("m_overflowMode", 0)
            wrap_flag = f.get("m_enableWordWrapping", None)
            wrapped = bool(wrap_flag) if wrap_flag is not None else (ovm == OVF_WRAP)
            raw = str(f.get("m_text", ""))
            content = raw.replace("\\n", "\n").strip()[:60] or "(空=运行时填充)"
            rows.append({**common, "kind": "TMP", "fontSize": fs, "auto": auto,
                         "wrap": wrapped, "ovm": ovm, "margin": (mx, my),
                         "text": content, "min": f.get("m_fontSizeMin", 0), "max": f.get("m_fontSizeMax", 0)})
        elif is_legacy:
            fd = f.get("m_FontData") or {}
            fs = fd.get("m_FontSize", 0)
            ovm = fd.get("m_HorizontalOverflow", 0)  # 0=Wrap 1=Overflow
            vovm = fd.get("m_VerticalOverflow", 0)
            raw = str(f.get("m_Text", "")).strip()[:60] or "(空=运行时填充)"
            rows.append({**common, "kind": "Text", "fontSize": fs, "auto": False,
                         "wrap": fd.get("m_WrapMode", 1) == 1 or ovm == 0, "ovm": ovm * 10 + vovm,
                         "margin": (0, 0), "text": raw.replace("\\n", "\n")[:60], "min": 0, "max": 0})
        elif is_tmp_input:
            # TMP_InputField：引用子文本组件，占位/文本在别处记录
            rows.append({**common, "kind": "InputField", "fontSize": "-", "auto": False,
                         "wrap": True, "ovm": "-", "margin": (0, 0), "text": "(输入框)", "min": 0, "max": 0})
    return rows


def max_safe_font(row) -> str:
    """估算可放大到的安全字号；返回 '建议值' 或 '不建议' 说明。"""
    if row["kind"] == "InputField" or row["fontSize"] in ("-", 0):
        return "-"
    if row["text"].startswith("(空") or row["text"] == "(输入框)":
        return "运行时决定"
    if row["auto"]:
        return "自动缩放(见min/max)"
    fs = float(row["fontSize"])
    w = row["rect_w"] - row["margin"][0]
    h = row["rect_h"] - row["margin"][1]
    if w <= 0 or h <= 0:
        return "容器为0"
    em = text_width_em(row["text"].replace("\n", ""))
    if em == 0:
        return "-"
    best = None
    for cand in [fs + 2, fs + 4, fs + 6, fs + 8, fs + 10, fs * 1.5, fs * 2.0]:
        if row["wrap"]:
            lines = wrap_lines(row["text"], w / cand)
            need_h = lines * LINE_H_EM * cand
            # 行宽不超（wrap 天然满足）且总高在容器内（留 2px 余量）
            if need_h <= h - 2:
                best = cand
        else:
            if em * cand <= w - 2 and LINE_H_EM * cand <= h - 2:
                best = cand
    if best is None:
        # 当前字号是否本来就紧张
        if row["wrap"]:
            lines = wrap_lines(row["text"], w / fs)
            if lines * LINE_H_EM * fs > h:
                return "当前已溢出!"
        return "不建议(无余量)"
    cur_lines = wrap_lines(row["text"], w / fs)
    new_lines = wrap_lines(row["text"], w / best)
    note = "" if cur_lines == new_lines else f"行数{cur_lines}->{new_lines}"
    return f"{best:g}{note}"


def main():
    all_rows = []
    for sc in sorted(SCENES.glob("M*.unity")):
        all_rows.extend(analyze_scene(sc))
    with OUT.open("w", encoding="utf-8") as fh:
        fh.write("# Scene 静态文本字号审计（自动生成，只读分析）\n\n")
        fh.write(f"- 行高系数按 {LINE_H_EM}em、CJK=1.0em、ASCII≈0.5~0.62em 保守估算；建议值留 ≥2px 余量。\n")
        fh.write(f"- 文本组件总数：{sum(1 for r in all_rows)}\n\n")
        cur_scene = None
        for r in all_rows:
            if r["scene"] != cur_scene:
                cur_scene = r["scene"]
                fh.write(f"\n## {cur_scene}\n\n")
                fh.write("| 层级路径 | 字号 | 容器WxH | 换行 | 溢出模式 | 文本(截断) | 估算可放大至 |\n")
                fh.write("|---|---|---|---|---|---|---|\n")
            fh.write(f"| `{r['path']}` | {r['fontSize']} | {r['rect_w']}x{r['rect_h']} "
                     f"| {'是' if r['wrap'] else '否'} | {r['ovm']} | {r['text'].replace('|','／').replace(chr(10),'⏎')} "
                     f"| {max_safe_font(r)} |\n")
    print(f"written {OUT}, rows={len(all_rows)}")


if __name__ == "__main__":
    main()
