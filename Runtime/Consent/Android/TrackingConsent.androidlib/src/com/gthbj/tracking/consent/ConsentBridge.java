package com.gthbj.tracking.consent;

import android.app.Activity;
import android.content.SharedPreferences;
import android.preference.PreferenceManager;

import com.google.android.ump.ConsentForm;
import com.google.android.ump.ConsentInformation;
import com.google.android.ump.ConsentRequestParameters;
import com.google.android.ump.FormError;
import com.google.android.ump.UserMessagingPlatform;

import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Google UMP 的薄封装。C# 侧 {@code Tracking.Consent.Android.UmpConsentPlatform} 按
 * **字符串类名**通过 JNI 调它。
 *
 * <p>🔴 **这个类存在的首要理由不是「方便」，是 R8。** UMP 的 aar 自带的
 * {@code proguard.txt} 只保住 proto 字段（{@code -keepclassmembers class * extends
 * ...consent_sdk.zzqm}），**没有一条保它的公开 API 类名**。所以若从 C# 直接按
 * {@code "com.google.android.ump.UserMessagingPlatform"} 找类，正式包
 * （arrows {@code AndroidMinifyRelease: 1}）里那个名字已经被改掉，
 * JNI 抛 ClassNotFoundException、被 catch 成一行警告，**同意流程整条静默失效**。
 * 写成 Java 之后这些都是**真引用**，R8 改名时会一起改，代码照样跑。
 *
 * <p>只剩本类与 {@link ConsentCallback} 自己要被 keep（C# 按名字找它们）——
 * 规则随模块走，见同目录 {@code proguard-consumer-rules.pro}。
 *
 * <p>🔴 **UMP 入口一律 {@code runOnUiThread}**，与 Google 自家 Unity 插件一致
 * （{@code GoogleMobileAds.Ump.Android.dll} 里能看到 {@code runOnUiThread} 与
 * {@code com.google.unity.ump.UnityConsentForm}）。代价是「发请求」被排到 UI 线程队列，
 * 于是 C# 调完 {@code requestConsentInfoUpdate} **立刻**读 {@link #canRequestAds()}
 * 可能还是旧值——这正是 PRD §6 那条快速路径要读的东西。**解法在 C# 那侧**：
 * {@code ConsentFlow.Tick()} 在请求在途期间每帧重读一次，不在这里阻塞 Unity 线程等 UI 线程
 * （启动期 UI 线程正忙，那是真会死锁的写法）。
 *
 * <p>同步 getter（{@link #canRequestAds()} 等）在 {@code consentInformation} 还是
 * {@code null} 时一律返回 {@code false}。**这不是兜底，就是 UMP 的真语义**：
 * 4.0.0 的 {@code consent_sdk.zzj.canRequestAds()} 先查一个「本进程调过更新请求没有」的
 * 布尔（{@code zzj.zzg}，在 {@code requestConsentInfoUpdate} 头上同步置位），没调过就直接假。
 */
public final class ConsentBridge {

    /**
     * 🔴 {@code volatile}：写在 UI 线程（{@link #requestConsentInfoUpdate} 的 Runnable 里），
     * 读在 Unity 线程（同步 getter）。没有它，Unity 线程可能一直看到 {@code null}，
     * 症状是「表单弹过了、广告却永远不放行」——没有任何报错。
     */
    private static volatile ConsentInformation consentInformation;

    /** 同上：{@link #loadConsentForm} 写、{@link #showConsentForm} 读，两次都在 UI 线程，但保持一致。 */
    private static volatile ConsentForm loadedForm;

    private ConsentBridge() {
    }

    /** {@code is_pub_misconfigured} ∨ 状态 ∈ {NOT_REQUIRED, OBTAINED}；没发过请求恒为假。 */
    public static boolean canRequestAds() {
        ConsentInformation info = consentInformation;
        try {
            return info != null && info.canRequestAds();
        } catch (Throwable error) {
            return false;
        }
    }

    /** 服务端判这次要弹表单。 */
    public static boolean isConsentFormRequired() {
        ConsentInformation info = consentInformation;
        try {
            return info != null
                    && info.getConsentStatus() == ConsentInformation.ConsentStatus.REQUIRED;
        } catch (Throwable error) {
            return false;
        }
    }

    /** 设置页要不要显示「隐私偏好」那一行。 */
    public static boolean isPrivacyOptionsRequired() {
        ConsentInformation info = consentInformation;
        try {
            return info != null
                    && info.getPrivacyOptionsRequirementStatus()
                        == ConsentInformation.PrivacyOptionsRequirementStatus.REQUIRED;
        } catch (Throwable error) {
            return false;
        }
    }

    /**
     * 读一条 IAB 串（{@code IABTCF_PurposeConsents} / {@code IABGPP_HDR_GppString}）。
     * UMP 与各家 SDK 都写在**默认** SharedPreferences 里，所以这里走
     * {@code PreferenceManager.getDefaultSharedPreferences}。取不到返回 {@code null}。
     */
    public static String readPreference(Activity activity, String key) {
        try {
            SharedPreferences preferences = PreferenceManager.getDefaultSharedPreferences(activity);
            return preferences.getString(key, null);
        } catch (Throwable error) {
            return null;
        }
    }

    /**
     * 发一次同意信息更新请求。参数照原包：**不设 debug geography、不设未成年标记**
     * （{@code new ConsentRequestParameters.Builder().build()}）。
     */
    public static void requestConsentInfoUpdate(final Activity activity, ConsentCallback callback) {
        final ConsentCallback once = new Once(callback);
        post(activity, once, new Runnable() {
            @Override
            public void run() {
                ConsentInformation info = UserMessagingPlatform.getConsentInformation(activity);
                consentInformation = info;
                info.requestConsentInfoUpdate(
                        activity,
                        new ConsentRequestParameters.Builder().build(),
                        new ConsentInformation.OnConsentInfoUpdateSuccessListener() {
                            @Override
                            public void onConsentInfoUpdateSuccess() {
                                once.onResult(null);
                            }
                        },
                        new ConsentInformation.OnConsentInfoUpdateFailureListener() {
                            @Override
                            public void onConsentInfoUpdateFailure(FormError formError) {
                                once.onResult(describe(formError));
                            }
                        });
            }
        });
    }

    /** 加载同意表单，成功后把它扣在 {@link #loadedForm} 上等 {@link #showConsentForm} 用。 */
    public static void loadConsentForm(final Activity activity, ConsentCallback callback) {
        final ConsentCallback once = new Once(callback);
        post(activity, once, new Runnable() {
            @Override
            public void run() {
                UserMessagingPlatform.loadConsentForm(
                        activity,
                        new UserMessagingPlatform.OnConsentFormLoadSuccessListener() {
                            @Override
                            public void onConsentFormLoadSuccess(ConsentForm form) {
                                loadedForm = form;
                                once.onResult(null);
                            }
                        },
                        new UserMessagingPlatform.OnConsentFormLoadFailureListener() {
                            @Override
                            public void onConsentFormLoadFailure(FormError formError) {
                                once.onResult(describe(formError));
                            }
                        });
            }
        });
    }

    /** 显示已加载的同意表单。 */
    public static void showConsentForm(final Activity activity, ConsentCallback callback) {
        final ConsentCallback once = new Once(callback);
        post(activity, once, new Runnable() {
            @Override
            public void run() {
                ConsentForm form = loadedForm;
                if (form == null) {
                    once.onResult("没有已加载的同意表单");
                    return;
                }

                // 表单是一次性的：show 之后这一份就作废了，留着只会在重试路径上被误用。
                loadedForm = null;
                form.show(activity, new ConsentForm.OnConsentFormDismissedListener() {
                    @Override
                    public void onConsentFormDismissed(FormError formError) {
                        once.onResult(describe(formError));
                    }
                });
            }
        });
    }

    /** 显示隐私选项表单（设置页入口）。UMP 自己负责加载它，不用先 load。 */
    public static void showPrivacyOptionsForm(final Activity activity, ConsentCallback callback) {
        final ConsentCallback once = new Once(callback);
        post(activity, once, new Runnable() {
            @Override
            public void run() {
                UserMessagingPlatform.showPrivacyOptionsForm(
                        activity,
                        new ConsentForm.OnConsentFormDismissedListener() {
                            @Override
                            public void onConsentFormDismissed(FormError formError) {
                                once.onResult(describe(formError));
                            }
                        });
            }
        });
    }

    /**
     * 把 UMP 调用排到 UI 线程，并把**同步抛出的异常**翻译成一次失败回调。
     *
     * <p>🔴 不抛给 JNI：调用方是冷启路径上的 C#，异常穿过 JNI 的后果不可控，
     * 而同意流程本来就是旁路——出错该降级成「请求失败」，不是崩掉一次正常的冷启。
     */
    private static void post(Activity activity, final ConsentCallback callback, final Runnable work) {
        if (activity == null) {
            callback.onResult("没有有效的 Activity");
            return;
        }

        try {
            activity.runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    try {
                        work.run();
                    } catch (Throwable error) {
                        callback.onResult(describe(error));
                    }
                }
            });
        } catch (Throwable error) {
            callback.onResult(describe(error));
        }
    }

    private static String describe(FormError formError) {
        if (formError == null) {
            return null;
        }

        return formError.getErrorCode() + ": " + formError.getMessage();
    }

    private static String describe(Throwable error) {
        return error.getClass().getName() + ": " + error.getMessage();
    }

    /**
     * 恰好回调一次的包装。
     *
     * <p>🔴 这是本桥对 {@code IConsentPlatform} 那条契约（「每个带回调的方法，恰好回调一次」）
     * 的兑现处。C# 那侧**没有**另加防御状态，就指望这里：多回一次会让重试次数超出策略，
     * 少回一次会让流程停在那儿不收尾。两条路径会撞上它——UMP 的监听器，
     * 以及 {@link #post} 里同步异常那条。
     */
    private static final class Once implements ConsentCallback {

        private final ConsentCallback target;
        private final AtomicBoolean fired = new AtomicBoolean(false);

        Once(ConsentCallback target) {
            this.target = target;
        }

        @Override
        public void onResult(String error) {
            if (fired.compareAndSet(false, true)) {
                target.onResult(error);
            }
        }
    }
}
