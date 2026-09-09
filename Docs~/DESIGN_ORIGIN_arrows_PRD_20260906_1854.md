# 来路：arrows PRD_20260906_1854

> 🔴 **2026-09-09 改名注记（本文其余部分是当时的原文，刻意不改写）**：包已由 `com.gthbj.level-tracking` 改名为 `com.gthbj.tracking`，内容切成四个程序集——`Tracking`（`Runtime/Core`，传输层）、`Tracking.Firebase.Android`（`Runtime/Firebase`，即下文的 `LevelTracking.Android`）、`Tracking.Editor`（`Editor`）、`LevelTracking`（`Runtime/Level`，关卡域）。**事件名 / 参数名 / 类型名一个没动**，下文的名字判据全部照旧成立。

本包由 `gthbj/arrows` 抽出，设计文档是 arrows 仓 `docs/prd/PRD_20260906_1854_关卡埋点公共库level-tracking与water_sort接入.md`
（rev.8，Codex 六轮评审后定稿；owner 2026-09-06 拍板仓库公开）。本文件只记「为什么长这样」的几条，不复述 PRD。

## 边界：机制进库，词汇留在游戏

- 进库：`IAnalyticsBackend` / `AnalyticsParameter` / `BufferedAnalyticsBackend` / `NullAnalyticsBackend`、`PlayClock`、
  `LevelTracker`、`LevelTrackingEvents`、`LevelTrackingSchema`、`FirebaseAnalyticsBackend`、`FirebaseAndroidConfig`。
- 留在游戏：装配根、停表理由位、游戏专属参数（arrows `source_level` / `level_corpus` / `arrows_left` / 主题；water_sort `source_level` / `moves`）、
  取值集合（`StartReasons` / `Boosters` 的值两游戏各不同）、Firebase SDK 本体、`google-services.json`、文档工具（各一份，读本包的表）。
- 为什么词汇表不整体共享：GA4 的 schema 改动不可逆，共享注册表会让游戏 B 改名损坏游戏 A 的历史。库只拥有它自己发出的名字。

## 可选参数为什么是不同的方法名

`LevelStartWithPrevFails` / `LevelEndWithFailCount` 不是重载：游戏侧的文档门按调用点的**方法名**判「这个游戏发不发它」，
带 `params` 的重载靠实参个数分不开（`LevelEnd(l, ok, failCount)` 与 `LevelEnd(l, ok, Of(moves, n))` 同为 3 元）。
也不用 `int?`——没有失败态的游戏（water_sort）不发占位 0。

## PlayClock 的三条不变量（arrows DECISION-20260901-11，真机 A/B 验过）

理由位集合而非计数器（停表会嵌套）；停表期间读数冻在停表那一刻；后台段在真实帧上结算并由「见过恢复」闸把门
（Unity 在 `OnApplicationPause(true)` 之后仍可能再跑一帧）。`Time.unscaledTime` **不**受 `maximumDeltaTime` 夹制，
不渲染帧不等于不计时——挂起整段会在恢复后的第一帧一次性补上，这正是要扣掉的那段。

## 证明手段

- 包内：`LevelTrackerEmitsExactlyItsSchema`（表 == 行为，双向）、`PlayClockTests`（三条不变量，三条变异各恰好打红一条）、名字合规。
- 游戏侧：`analytics_doc.py` 按钉住的 SHA 读 `LevelTrackingSchema.cs`，调用点门按方法名，渲染结果以 CSV 提交当回执；飞书 / GA4 各靠回读。
- `LevelTracking.Android` 只有 APK 能编译，两游戏的验收都带 APK。
