using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tracking.Consent
{
    /// <summary>
    /// 一段 uGUI 正文里的几个词画下划线、各自可点（首启条款弹窗的两个文件名）。量法来自 arrows 的 `FirstRunConsentDialog`：
    /// 按词在正文里的字符下标去排版结果里取字盒。下划线与正文同色；热区是透明底的 <see cref="Button"/>。
    ///
    /// 🔴 **位置在 LateUpdate 里按渲染时的缩放现量，缩放一变就整组重建，不在建弹窗时量**：弹窗多半在 Awake 里建，
    /// 那时 CanvasScaler 还没生效（画布缩放是 1，1440 宽的机器渲染前才变 1.33）；截图工具也常先建、再换尺寸渲染。
    /// 字的步进按像素取整，换了缩放词就挪位，建弹窗时量的线会画到旁边的字上（sudoku 2026-09-24 截图实证）。
    /// </summary>
    public sealed class TextLinks : MonoBehaviour
    {
        private const float UnderlineGap = 6f;
        private const float UnderlineThickness = 4f;

        /// <summary>热区在墨迹之外上下各留的余量：一行字的墨迹才三十来个单位高，太薄不好点（arrows 同值）。</summary>
        private const float TapPadY = 14f;

        private readonly List<GameObject> built = new List<GameObject>();
        private (string Word, Action OnClick)[] links;
        private Text text;
        private float measuredAt;

        /// <summary>
        /// 给 <paramref name="text"/> 挂上链接。它会先改写正文（见 <see cref="FreezeLines"/>），所以要在量行高、摆版式之前调；
        /// 每个词都必须原样出现在正文里。
        /// </summary>
        public static void Attach(Text text, float wrapWidth, params (string Word, Action OnClick)[] links)
        {
            FreezeLines(text, wrapWidth, Array.ConvertAll(links, link => link.Word));
            var component = text.gameObject.AddComponent<TextLinks>();
            component.text = text;
            component.links = links;
        }

        /// <summary>
        /// 链接词被折行劈开就把它整条推到下一行（arrows 的 KeepDocumentNamesOnOneLine：拉丁文在空格处断，
        /// 中日文任意两个字之间都能断），再把每一行写死成 `\n`、关掉自动折行：正文高度与每个词落在哪一行就都定住了。
        /// 🔴 **按 1 像素 / 单位折**，不按当时的画布缩放（见类注释，那时的缩放不作数）：同一句话在每台机器、每张截图里
        /// 折法都一样。代价是别的缩放下字宽取整不同，行尾可能出界几个单位，弹窗两侧留白要盖得住。
        /// </summary>
        private static void FreezeLines(Text text, float width, string[] words)
        {
            // 词的位置按字符下标量，rich text 一开下标就与可见字符错位。
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            var settings = text.GetGenerationSettings(new Vector2(width, 0));
            settings.scaleFactor = 1;
            var generator = new TextGenerator();

            IList<UILineInfo> Lines()
            {
                generator.Populate(text.text, settings);
                return generator.lines;
            }

            // 每轮推一个被劈开的词；推过的词前面就是换行、不会再被推，所以至多 words.Length 轮。
            for (var pass = 0; pass < words.Length; pass++)
            {
                var lines = Lines();
                var split = Array.Find(words, word =>
                {
                    var at = text.text.IndexOf(word, StringComparison.Ordinal);
                    return at > 0 && text.text[at - 1] != '\n' && LineOf(lines, at) != LineOf(lines, at + word.Length - 1);
                });
                if (split == null)
                {
                    break;
                }

                var index = text.text.IndexOf(split, StringComparison.Ordinal);
                text.text = text.text.Substring(0, index).TrimEnd(' ') + "\n" + text.text.Substring(index);
            }

            var final = Lines();
            if (final.Count == 0)
            {
                return;
            }

            var rows = new string[final.Count];
            for (var i = 0; i < rows.Length; i++)
            {
                var end = i + 1 < rows.Length ? final[i + 1].startCharIdx : text.text.Length;
                rows[i] = text.text.Substring(final[i].startCharIdx, end - final[i].startCharIdx).TrimEnd(' ', '\n');
            }

            text.text = string.Join("\n", rows);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void LateUpdate()
        {
            var scale = text.pixelsPerUnit;
            if (scale == measuredAt)
            {
                return;
            }

            measuredAt = scale;
            foreach (var old in built)
            {
                Destroy(old);
            }

            built.Clear();
            var generator = text.cachedTextGenerator;
            generator.Populate(text.text, text.GetGenerationSettings(text.rectTransform.rect.size));
            foreach (var (word, onClick) in links)
            {
                var at = text.text.IndexOf(word, StringComparison.Ordinal);
                if (at < 0 || at + word.Length > generator.characterCount)
                {
                    Debug.LogError($"正文里找不到链接词「{word}」：{text.text}");
                    continue;
                }

                var ink = InkBox(generator, at, word.Length, 1f / scale);
                var hotspot = Child(word);
                hotspot.gameObject.AddComponent<Image>().color = Color.clear; // 透明也接射线
                hotspot.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
                Place(hotspot, new Rect(ink.xMin, ink.yMin - TapPadY, ink.width, ink.height + TapPadY * 2));

                var underline = Child(word + " Underline");
                var bar = underline.gameObject.AddComponent<Image>();
                bar.color = text.color;
                bar.raycastTarget = false;
                Place(underline, new Rect(ink.xMin, ink.yMin - UnderlineGap - UnderlineThickness, ink.width, UnderlineThickness));
            }
        }

        private RectTransform Child(string name)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(transform, false);
            built.Add(rt.gameObject);
            return rt;
        }

        /// <summary>锚点取正文自己的 pivot：排版结果的原点就在 pivot 上（<c>GetGenerationSettings</c> 把它传了进去）。</summary>
        private void Place(RectTransform rt, Rect local)
        {
            rt.anchorMin = rt.anchorMax = text.rectTransform.pivot;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = local.center;
            rt.sizeDelta = local.size;
        }

        /// <summary>
        /// 一个词占的矩形（正文局部坐标；排版结果是像素，乘 <paramref name="unitsPerPixel"/> 换回单位）。
        /// 横向取步进盒，盖住整个词含字间空隙；纵向取这几个字自己的墨迹，线才贴在字底下——用行盒的话行盒含降部与行距，
        /// 线落在字中间，看着是删除线（arrows 实拍）。🔴 空格不出四边形，字符下标不能拿去索引 verts，只能按位置挑墨迹。
        /// </summary>
        private static Rect InkBox(TextGenerator generator, int start, int length, float unitsPerPixel)
        {
            var characters = generator.characters;
            var left = float.MaxValue;
            var right = float.MinValue;
            for (var i = start; i < start + length; i++)
            {
                left = Mathf.Min(left, characters[i].cursorPos.x);
                right = Mathf.Max(right, characters[i].cursorPos.x + characters[i].charWidth);
            }

            var line = generator.lines[LineOf(generator.lines, start)];
            var bottom = float.MaxValue;
            var top = float.MinValue;
            var verts = generator.verts;
            for (var quad = 0; quad + 3 < verts.Count; quad += 4)
            {
                var upper = verts[quad].position;
                var lower = verts[quad + 2].position;
                var center = (upper + lower) * 0.5f;
                if (Mathf.Approximately(upper.y, lower.y) || center.x < left || center.x > right
                    || center.y > line.topY || center.y < line.topY - line.height)
                {
                    continue;
                }

                bottom = Mathf.Min(bottom, Mathf.Min(upper.y, lower.y));
                top = Mathf.Max(top, Mathf.Max(upper.y, lower.y));
            }

            if (bottom > top)
            {
                // 一点墨迹都没挑到（不该发生：词是实打实的字）。退回行盒，至少还有块热区。
                top = line.topY;
                bottom = line.topY - line.height;
            }

            return Rect.MinMaxRect(left * unitsPerPixel, bottom * unitsPerPixel, right * unitsPerPixel, top * unitsPerPixel);
        }

        private static int LineOf(IList<UILineInfo> lines, int characterIndex)
        {
            var line = 0;
            while (line + 1 < lines.Count && lines[line + 1].startCharIdx <= characterIndex)
            {
                line++;
            }

            return line;
        }
    }
}
