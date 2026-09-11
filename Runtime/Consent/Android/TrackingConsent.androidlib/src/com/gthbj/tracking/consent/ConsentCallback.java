package com.gthbj.tracking.consent;

/**
 * 桥回调。C# 侧用 {@code AndroidJavaProxy} 按**字符串接口名**实现它，
 * 所以它必须逃过 R8 改名——keep 规则见同目录 {@code proguard-consumer-rules.pro}。
 *
 * <p>🔴 **故意不是嵌套接口**：嵌套的话 JNI 名字带 {@code $}
 * （{@code com.gthbj.tracking.consent.ConsentBridge$Callback}），
 * 而 C# 那侧写错一个字符的症状是「代理建不出来」→ 回调永不到达 → 流程静静停住。
 * 顶层接口没有这个雷。
 *
 * <p>🔴 **回调线程是 Android 主线程（UI 线程），不是 Unity 主线程**：UMP 的监听器由
 * {@code Handler.post} 投递（4.0.0 实测 {@code consent_sdk.zzw} 内部持一个主 Looper
 * 的 Handler）。C# 那侧因此**必须**把它排队、等 {@code Pump()} 在 Unity 线程上交付，
 * 不能在这里直接碰 Unity API。
 */
public interface ConsentCallback {

    /**
     * 一次操作结束。
     *
     * @param error 出错时是一句人话；{@code null} 表示成功。
     */
    void onResult(String error);
}
