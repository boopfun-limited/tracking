using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tracking.Consent;
using UnityEngine;
using UnityEngine.UI;

namespace LevelTracking.Tests.EditMode
{
    /// <summary>
    /// 首启条款弹窗的字与下划线链接（<see cref="TermsCopy"/> / <see cref="TextLinks"/>，D-20260924-01）。
    /// 🔴 这两个类引 uGUI，**不在 CLAUDE.md 那道 Roslyn 秒级门里**，只能由消费方工程带起来跑。
    /// </summary>
    public class TermsCopyTests
    {
        /// <summary>
        /// 14 种语言一组不少，每组五个字段都有、正文两个占位符都在。JsonUtility 对拼错 / 漏写的 key 是静默留 null，
        /// 弹窗上就是一块空白；少了占位符，那个文件名就不在正文里、也就没有链接。
        /// </summary>
        [Test]
        public void EveryLanguageHasTheWholeDialog()
        {
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "English", "ChineseSimplified", "ChineseTraditional", "Japanese", "Korean", "German", "French",
                    "Spanish", "Portuguese", "Italian", "Dutch", "Polish", "Turkish", "Indonesian",
                },
                TermsCopy.All.Select(pack => pack.language));
            foreach (var pack in TermsCopy.All)
            {
                foreach (var text in new[] { pack.consentTitle, pack.consentBody, pack.consentAccept, pack.consentTermsLabel, pack.consentPrivacyLabel })
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(text), $"{pack.language} 有字段是空的");
                }

                StringAssert.Contains("{0}", pack.consentBody, pack.language);
                StringAssert.Contains("{1}", pack.consentBody, pack.language);
            }
        }

        [Test]
        public void UnknownLanguageGetsEnglish()
        {
            Assert.AreEqual("English", TermsCopy.For("Klingon").language);
            Assert.AreEqual("Japanese", TermsCopy.For("Japanese").language);
        }

        /// <summary>
        /// 两个文件名各得一块热区，点哪块开哪份；热区中心落在那个词所在的那一行、那几个字的横向范围里
        /// （锚点没取正文的 pivot 的话，整块会偏开半个正文高）。LateUpdate 在 EditMode 不跑，这里手动量一次。
        /// </summary>
        [Test]
        public void EachDocumentNameOpensItsOwnDocument()
        {
            var canvas = new GameObject("Canvas", typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var body = new GameObject("Body", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                body.transform.SetParent(canvas.transform, false);
                body.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                body.fontSize = 36;
                body.alignment = TextAnchor.UpperCenter;
                body.verticalOverflow = VerticalWrapMode.Overflow;
                body.rectTransform.pivot = new Vector2(0.5f, 1f);

                var copy = TermsCopy.For("English");
                var opened = new List<string>();
                copy.ShowBody(body, 792, () => opened.Add("terms"), () => opened.Add("privacy"));
                body.rectTransform.sizeDelta = new Vector2(792, body.preferredHeight);
                typeof(TextLinks).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(body.GetComponent<TextLinks>(), null);

                var zones = body.GetComponentsInChildren<Button>();
                Assert.AreEqual(2, zones.Length);
                var layout = body.cachedTextGenerator;
                var words = new[] { copy.consentTermsLabel, copy.consentPrivacyLabel };
                for (var i = 0; i < words.Length; i++)
                {
                    var at = body.text.IndexOf(words[i], System.StringComparison.Ordinal);
                    var last = layout.characters[at + words[i].Length - 1];
                    var line = layout.lines[layout.lines.Count(l => l.startCharIdx <= at) - 1];
                    // 排版结果是像素，热区中心也换成像素再比（这块画布没有 CanvasScaler，其实是 1 倍）。
                    var center = body.rectTransform.InverseTransformPoint(zones[i].transform.position) * body.pixelsPerUnit;
                    Assert.That(center.x, Is.InRange(layout.characters[at].cursorPos.x, last.cursorPos.x + last.charWidth), $"{words[i]} 的热区不在这个词上");
                    Assert.That(center.y, Is.InRange(line.topY - line.height, line.topY), $"{words[i]} 的热区不在它那一行");
                    zones[i].onClick.Invoke();
                }

                CollectionAssert.AreEqual(new[] { "terms", "privacy" }, opened);
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }
    }
}
