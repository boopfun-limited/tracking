package com.gthbj.tracking.identity;

/** One native completion; volatile publication makes polling non-blocking. No managed callback. */
public final class AppSetIdResult {
    private volatile boolean done;
    private boolean successful;
    private int scope;
    private String id;
    synchronized void complete(boolean success, int scope, String id) {
        if (done) return;
        this.successful = success;
        this.scope = scope;
        this.id = id;
        done = true;
    }
    public boolean isDone() { return done; }
    public boolean isSuccessful() { return successful; }
    public int getScope() { return scope; }
    public String getId() { return id; }
}
