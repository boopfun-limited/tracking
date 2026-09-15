# 随模块传递给消费方 app 的 R8 的 keep 规则（build.gradle 的 consumerProguardFiles）。
#
# 🔴 **只保这一个，别扩**。Firebase 自己的类不需要 keep：FirebaseConsentBridge.java 里对它们是
# **真 Java 引用**，R8 改名时会一起改。要 keep 的只有「C# 按字符串名字找」的桥类——JNI 那条路
# R8 看不见，改了名就是 ClassNotFoundException，而症状是静默的（被 catch 成一行警告）。
#
# 验收：在**正式包**的 dex 里查描述符
#   Lcom/gthbj/tracking/firebase/FirebaseConsentBridge;
# 还在不在。构建绿证明不了这一面。

-keep class com.gthbj.tracking.firebase.FirebaseConsentBridge { *; }
