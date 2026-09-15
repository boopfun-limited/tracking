package com.gthbj.tracking.firebase;

import android.content.Context;

import com.google.firebase.analytics.FirebaseAnalytics;

import java.util.EnumMap;
import java.util.Map;

/**
 * Firebase 同意值的 Java 桥：只有一个静态方法，C# 在 {@code FirebaseAnalyticsBackend.AttachWhenReady}
 * 一进来就同步调它（D-20260916-01）。
 *
 * <p>🔴 <b>为什么走 Java 而不是 Firebase 的 C# API</b>：C# 那侧要等
 * {@code FirebaseApp.CheckAndFixDependenciesAsync} 回调才能用（慢机一两秒），而老玩家的同意请求在
 * 首个场景头几帧就发——arrows 真机逐帧录冷启，进程起来约 0.9 秒，UMP 再约 0.6 秒回话并把玩家的真实选择
 * 推给 Firebase。在回调里写死 GRANTED 会落在那次推送之后，把拒绝盖回「已同意」一整场。Java 侧的 Firebase
 * 在 {@code FirebaseInitProvider} 里就初始化好了，这里同步写，时点与原包 oakever 在
 * {@code Application.onCreate} 里写的一样，天然排在最前。
 *
 * <p>写的内容照原包（1.29.1 jadx {@code f9/c.java:143-151}、{@code l9/g.java}）：开采集 + 四项同意全 GRANTED。
 */
public final class FirebaseConsentBridge {

    private FirebaseConsentBridge() {
    }

    /** 开采集，并把 ad_storage / analytics_storage / ad_user_data / ad_personalization 写成 GRANTED。 */
    public static void grantAll(Context context) {
        FirebaseAnalytics analytics = FirebaseAnalytics.getInstance(context.getApplicationContext());
        analytics.setAnalyticsCollectionEnabled(true);

        Map<FirebaseAnalytics.ConsentType, FirebaseAnalytics.ConsentStatus> consent =
                new EnumMap<>(FirebaseAnalytics.ConsentType.class);
        consent.put(FirebaseAnalytics.ConsentType.AD_STORAGE, FirebaseAnalytics.ConsentStatus.GRANTED);
        consent.put(FirebaseAnalytics.ConsentType.ANALYTICS_STORAGE, FirebaseAnalytics.ConsentStatus.GRANTED);
        consent.put(FirebaseAnalytics.ConsentType.AD_USER_DATA, FirebaseAnalytics.ConsentStatus.GRANTED);
        consent.put(FirebaseAnalytics.ConsentType.AD_PERSONALIZATION, FirebaseAnalytics.ConsentStatus.GRANTED);
        analytics.setConsent(consent);
    }
}
