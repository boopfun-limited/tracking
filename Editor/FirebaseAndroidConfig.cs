using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using Debug = UnityEngine.Debug;

namespace LevelTracking.Editor
{
    /// <summary>
    /// 把 `google-services.json` 转成 Android 资源，供 Firebase 运行时读取。
    ///
    /// 🔴 **为什么这一步要我们自己做，而不是让 Firebase 的编辑器插件做**（2026-08-31 实测）：
    /// `Firebase.Editor.dll` 用它内部的 `PythonExecutor` 调本目录那支
    /// `generate_xml_from_google_services_json.py`，而它的候选解释器名单里**只有 `python` 一个**。
    /// 现代 macOS 早已移除 Python 2，本机只有 `python3`，于是它找不到解释器——
    /// **然后什么都不做，连一行日志都不打**。
    ///
    /// 失败形态是最坏的那种：APK 照常构建成功、Firebase 的原生库与 dex 类一应俱全，
    /// 只是包里没有 `google_app_id`，`FirebaseApp` 在真机上初始化失败、一条埋点都发不出去。
    /// 构建全绿、包能装能玩、数据永远是 0，而没有任何一处会告诉你。
    ///
    /// 所以这里**显式用 `python3` 调 Google 自己那支脚本**（不重写它的映射逻辑，
    /// 免得两份实现漂移），并在生成后校验产物真的含本次构建的 application id。
    /// </summary>
    public static class FirebaseAndroidConfig
    {
        public const string GoogleServicesJsonPath = "Assets/google-services.json";

        public const string GeneratorScriptPath =
            "Assets/Firebase/Editor/generate_xml_from_google_services_json.py";

        public const string AndroidLibraryPath = "Assets/Plugins/Android/FirebaseApp.androidlib";

        /// <summary>
        /// androidlib 的 manifest package（AGP 8 拿它当 namespace）。🔴 **固定值、与 application id 无关**：
        /// Unity 只在**第一次**导出这个模块时生成它的 `build.gradle`（namespace 抄当时的 package），
        /// 之后增量导出不再改；package 若随 application id 变（测试包 / 正式包交替构建），第二次起
        /// AGP 就报 `Incorrect package="…" found in source AndroidManifest.xml`（2026-09-06 实踩）。
        /// 这个模块只装资源、没有代码，namespace 叫什么都行，所以取一个不含任何游戏包名的常量。
        /// </summary>
        public const string ManifestPackage = "com.gthbj.leveltracking.firebaseconfig";


        /// <summary>
        /// 按本次构建**实际生效**的 application id 重建 androidlib。🔴 包不认识任何游戏的包名：
        /// 「这一档（QA / 管理员包）要不要配 Firebase」是**宿主构建脚本的分支**——不配就别调本方法，
        /// 并自己把上一次构建留下的 <see cref="AndroidLibraryPath"/> 删掉（见 <see cref="Remove"/>）。
        /// json 里没有这个 id 时本方法抛 <c>BuildFailedException</c>，不静默。
        ///
        /// 🔴 无条件先删再建，不是图省事：这个目录住在 `Assets/` 下、跨构建留存，
        /// 上一次构建（比如测试包）留下的 `google_app_id` 会被这一次（比如管理员包）原样带走，
        /// 于是两个包的数据混进同一个 Firebase app——而包能装、能跑、数据也在流，
        /// 只是流错了地方。
        /// </summary>
        public static void Regenerate(string applicationId)
        {
            if (string.IsNullOrWhiteSpace(applicationId))
            {
                throw new BuildFailedException("Firebase 配置生成需要一个非空的 application id。");
            }

            if (Directory.Exists(AndroidLibraryPath))
            {
                AssetDatabase.DeleteAsset(AndroidLibraryPath);
            }


            RequireFile(GoogleServicesJsonPath, "Firebase 客户端配置");
            RequireFile(GeneratorScriptPath, "Firebase 的 xml 生成脚本");

            var valuesDirectory = Path.Combine(AndroidLibraryPath, "res", "values");
            Directory.CreateDirectory(valuesDirectory);
            var xmlPath = Path.Combine(valuesDirectory, "google-services.xml");

            RunGenerator(applicationId, xmlPath);
            VerifyGenerated(xmlPath, applicationId);
            WriteLibraryScaffolding();
            DropStaleGeneratedModule();

            AssetDatabase.Refresh();
            Debug.Log($"LEVEL_TRACKING_FIREBASE_CONFIG generated applicationId={applicationId} xml={xmlPath}");
        }

        /// <summary>
        /// 宿主决定这一档不配 Firebase 时调它：删掉上一次构建留下的 androidlib，免得别的包名的
        /// `google_app_id` 被原样带走——那种错的表现是「数据流进了别的 app」。
        /// </summary>
        public static void Remove(string applicationId, string reason)
        {
            if (Directory.Exists(AndroidLibraryPath))
            {
                AssetDatabase.DeleteAsset(AndroidLibraryPath);
            }
            AssetDatabase.Refresh();
            Debug.Log($"LEVEL_TRACKING_FIREBASE_CONFIG skipped applicationId={applicationId} reason={reason}");
        }

        private static void RunGenerator(string applicationId, string xmlPath)
        {
            var info = new ProcessStartInfo
            {
                // 🔴 写死 `python3`：这一步存在的全部理由就是 Firebase 只会找 `python`。
                FileName = "python3",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            info.ArgumentList.Add(GeneratorScriptPath);
            info.ArgumentList.Add("-i");
            info.ArgumentList.Add(GoogleServicesJsonPath);
            info.ArgumentList.Add("-o");
            info.ArgumentList.Add(xmlPath);
            info.ArgumentList.Add("-p");
            info.ArgumentList.Add(applicationId);

            using (var process = Process.Start(info))
            {
                if (process == null)
                {
                    throw new BuildFailedException("起不来 python3——Firebase 的 Android 配置无法生成。");
                }

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                // 🔴 **不看退出码**：这支脚本在「json 里没有匹配包名」时照样退 0，
                // 只把原因打在 stdout 上。判据只能是产物本身。
                if (!File.Exists(xmlPath))
                {
                    throw new BuildFailedException(
                        $"Firebase 配置没生成出来（applicationId={applicationId}）。"
                        + $"\nstdout: {stdout.Trim()}\nstderr: {stderr.Trim()}");
                }
            }
        }

        private static void VerifyGenerated(string xmlPath, string applicationId)
        {
            var xml = File.ReadAllText(xmlPath);
            foreach (var required in new[] { "google_app_id", "gcm_defaultSenderId", "project_id" })
            {
                if (!xml.Contains(required))
                {
                    throw new BuildFailedException(
                        $"生成的 Firebase 配置缺 `{required}`（applicationId={applicationId}）："
                        + "运行时 FirebaseApp 会初始化失败，而包照样能装能跑。");
                }
            }
        }

        private static void WriteLibraryScaffolding()
        {
            File.WriteAllText(
                Path.Combine(AndroidLibraryPath, "AndroidManifest.xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
                + "<!-- 构建期生成，请勿手改：LevelTracking.Editor.FirebaseAndroidConfig 每次构建重建本目录。 -->\n"
                + "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"\n"
                + $"          package=\"{ManifestPackage}\">\n"
                + "</manifest>\n");

            File.WriteAllText(
                Path.Combine(AndroidLibraryPath, "project.properties"),
                "# 构建期生成，请勿手改。Unity 靠这个文件把目录识别成 Android library 模块。\n"
                + "android.library=true\n"
                + "target=android-36\n");
        }

        /// <summary>
        /// 清掉 Unity 上一次导出留下的、namespace 与 <see cref="ManifestPackage"/> 不一致的生成模块
        /// （`Library/Bee/Android/Prj/<backend>/Gradle/unityLibrary/FirebaseApp.androidlib`），让它重新生成。
        /// 只会在改过 package 名之后命中一次（比如从游戏自己那版生成器迁到本包），平时是空操作。
        /// 不清的话失败形态是 gradle 的 `Incorrect package=` 而不是任何指向这里的提示。
        /// </summary>
        private static void DropStaleGeneratedModule()
        {
            var projects = Path.Combine("Library", "Bee", "Android", "Prj");
            if (!Directory.Exists(projects))
            {
                return;
            }

            var libraryName = Path.GetFileName(AndroidLibraryPath);
            foreach (var backend in Directory.GetDirectories(projects))
            {
                var module = Path.Combine(backend, "Gradle", "unityLibrary", libraryName);
                var gradle = Path.Combine(module, "build.gradle");
                if (!File.Exists(gradle) || File.ReadAllText(gradle).Contains($"namespace \"{ManifestPackage}\""))
                {
                    continue;
                }

                Directory.Delete(module, recursive: true);
                Debug.Log($"LEVEL_TRACKING_FIREBASE_CONFIG dropped stale generated module {module} (namespace != {ManifestPackage})");
            }
        }

        private static void RequireFile(string path, string what)
        {
            if (!File.Exists(path))
            {
                throw new BuildFailedException($"{what}不在：{path}");
            }
        }
    }
}
