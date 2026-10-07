package com.gthbj.tracking.consent;

import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.atomic.AtomicBoolean;

/** Keeps UMP's UI thread out of Unity's managed runtime until the Unity thread polls. */
final class ConsentResultQueue {
    private final ConcurrentLinkedQueue<Result> results = new ConcurrentLinkedQueue<>();

    ConsentCallback once(final ConsentCallback target) {
        return new ConsentCallback() {
            private final AtomicBoolean fired = new AtomicBoolean(false);

            @Override
            public void onResult(String error) {
                if (fired.compareAndSet(false, true)) {
                    results.add(new Result(target, error));
                }
            }
        };
    }

    // Called through JNI by UmpConsentPlatform.Pump on the Unity thread. Invoking
    // the proxy here avoids a UI-thread nativeProxyInvoke during GC or suspension.
    int dispatch() {
        int delivered = 0;
        Result result;
        while ((result = results.poll()) != null) {
            result.target.onResult(result.error);
            delivered++;
        }
        return delivered;
    }

    private static final class Result {
        final ConsentCallback target;
        final String error;

        Result(ConsentCallback target, String error) {
            this.target = target;
            this.error = error;
        }
    }
}
