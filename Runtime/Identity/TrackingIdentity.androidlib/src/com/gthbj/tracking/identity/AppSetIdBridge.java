package com.gthbj.tracking.identity;

import android.content.Context;
import com.google.android.gms.appset.AppSet;
import com.google.android.gms.appset.AppSetIdInfo;

public final class AppSetIdBridge {
    public static AppSetIdResult request(Context context) {
        AppSetIdResult result = new AppSetIdResult();
        try {
            AppSet.getClient(context.getApplicationContext()).getAppSetIdInfo().addOnCompleteListener(task -> {
                if (task.isSuccessful()) {
                    AppSetIdInfo info = task.getResult();
                    result.complete(info != null, info == null ? 0 : info.getScope(), info == null ? null : info.getId());
                } else result.complete(false, 0, null);
            });
        } catch (Exception ignored) { result.complete(false, 0, null); }
        return result;
    }
}
