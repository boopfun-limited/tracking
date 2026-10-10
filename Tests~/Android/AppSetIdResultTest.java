package com.gthbj.tracking.identity;
/** Tests cross-thread publication, duplicate completion and failure, without Android dependencies. GPT-6. */
public final class AppSetIdResultTest {
    static void check(boolean value) { if (!value) throw new AssertionError(); }
    public static void main(String[] args) throws Exception {
        for(int i=0;i<1000;i++) {
            AppSetIdResult r=new AppSetIdResult();
            Thread producer=new Thread(() -> { r.complete(true,2,"test-value"); r.complete(false,0,null); });
            producer.start();
            long end=System.nanoTime()+1000000000L;
            while(!r.isDone() && System.nanoTime()<end) Thread.yield();
            check(r.isDone() && r.isSuccessful() && r.getScope()==2 && "test-value".equals(r.getId()));
            producer.join();
        }
        AppSetIdResult r=new AppSetIdResult();r.complete(false,0,null);r.complete(true,2,"late");
        check(r.isDone() && !r.isSuccessful() && r.getId()==null);
        System.out.println("PASS App Set ID: cross-thread publication, first completion wins, failure");
    }
}
