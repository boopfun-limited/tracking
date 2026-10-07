package com.gthbj.tracking.consent;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.atomic.AtomicInteger;

/** Standalone JVM regression tests for the queue used by the Android UMP bridge. GPT-6. */
public final class ConsentResultQueueTest {
    private static void check(boolean ok, String message) {
        if (!ok) throw new AssertionError(message);
    }

    public static void main(String[] args) throws Exception {
        deferredAndOrdered();
        racingDuplicateIsDeliveredOnce();
        throwingResultDoesNotDiscardLaterResults();
        reentrantResultRemainsOnConsumer();
        System.out.println("PASS: 4 consent result queue regressions");
    }

    private static void deferredAndOrdered() throws Exception {
        ConsentResultQueue queue = new ConsentResultQueue();
        List<String> errors = new ArrayList<>();
        List<Thread> threads = new ArrayList<>();
        ConsentCallback success = queue.once(error -> { errors.add(error); threads.add(Thread.currentThread()); });
        ConsentCallback failure = queue.once(error -> { errors.add(error); threads.add(Thread.currentThread()); });
        Thread ui = new Thread(() -> { success.onResult(null); success.onResult("duplicate"); failure.onResult("failure"); }, "UI");
        ui.start(); ui.join(2000);
        check(!ui.isAlive(), "UI producer must return without waiting for the consumer");
        check(errors.isEmpty(), "UI producer entered the callback before polling");
        queue.dispatch();
        check(errors.size() == 2 && errors.get(0) == null && "failure".equals(errors.get(1)), "success/error values and FIFO order");
        check(threads.stream().allMatch(thread -> thread == Thread.currentThread()), "callbacks must run on polling thread");
        success.onResult("late duplicate"); queue.dispatch();
        check(errors.size() == 2, "once must survive dispatch");
    }

    private static void racingDuplicateIsDeliveredOnce() throws Exception {
        ConsentResultQueue queue = new ConsentResultQueue();
        AtomicInteger count = new AtomicInteger();
        ConsentCallback once = queue.once(error -> count.incrementAndGet());
        CountDownLatch start = new CountDownLatch(1);
        List<Thread> producers = new ArrayList<>();
        for (int i = 0; i < 16; i++) {
            Thread thread = new Thread(() -> {
                try { start.await(); } catch (InterruptedException e) { throw new AssertionError(e); }
                once.onResult(null);
            });
            producers.add(thread); thread.start();
        }
        start.countDown();
        for (Thread thread : producers) thread.join();
        check(count.get() == 0, "racing producers must not call target");
        queue.dispatch(); check(count.get() == 1, "duplicate completion race");
    }

    private static void throwingResultDoesNotDiscardLaterResults() {
        ConsentResultQueue queue = new ConsentResultQueue();
        AtomicInteger count = new AtomicInteger();
        queue.once(error -> { throw new IllegalStateException("test"); }).onResult(null);
        queue.once(error -> count.incrementAndGet()).onResult(null);
        try { queue.dispatch(); throw new AssertionError("expected callback failure"); }
        catch (IllegalStateException expected) { }
        queue.dispatch(); check(count.get() == 1, "later results must remain available after a failed dispatch");
    }

    private static void reentrantResultRemainsOnConsumer() {
        ConsentResultQueue queue = new ConsentResultQueue();
        AtomicInteger count = new AtomicInteger();
        queue.once(error -> {
            count.incrementAndGet();
            queue.once(next -> count.incrementAndGet()).onResult(null);
        }).onResult(null);
        queue.dispatch(); check(count.get() == 2, "reentrant result delivery");
    }
}
