# 随模块传递给消费方 app 的 R8 的 keep 规则（build.gradle 的 consumerProguardFiles）。
#
# 🔴 **只保这两个，别扩**。UMP 自己那一堆类不需要 keep：ConsentBridge.java 里对它们是
# **真 Java 引用**，R8 改名时会一起改，代码照样跑。要 keep 的只有「C# 按字符串名字找」的这两个
# ——JNI 那条路 R8 看不见，改了名就是 ClassNotFoundException，而症状是静默的
# （被 catch 成一行警告，构建 / 安装 / EditMode 全绿）。
#
# 验收方式见包 README「同意模块的 Android 侧」：在**正式包**的 dex 里查描述符
#   Lcom/gthbj/tracking/consent/ConsentBridge;
# 还在不在。构建绿证明不了这一面。

-keep class com.gthbj.tracking.consent.ConsentBridge { *; }

# AndroidJavaProxy 在运行期按这个接口名建动态代理，方法名也按名字派发。
-keep interface com.gthbj.tracking.consent.ConsentCallback { *; }
