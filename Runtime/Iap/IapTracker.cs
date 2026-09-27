using System;
using System.Collections.Generic;

namespace Tracking.Iap
{
    public enum IapResult { Unavailable, Succeeded, Failed, Cancelled, Pending, NotOwned }

    public sealed class IapState
    {
        public bool IsOwned { get; }
        public string StoreStatus { get; }
        public string PriceLabel { get; }
        public IapState(bool isOwned, string storeStatus, string priceLabel)
        { IsOwned = isOwned; StoreStatus = storeStatus ?? ""; PriceLabel = priceLabel ?? ""; }
    }

    /// <summary>One session-lifetime observer per non-consumable product; no store or Unity dependency.</summary>
    public sealed class IapTracker : IDisposable
    {
        internal sealed class Context
        {
            internal string Flow = "", Attempt = "", Source = "store_sync", Price = "";
            internal AnalyticsParameter[] Extra = Array.Empty<AnalyticsParameter>();
        }
        private readonly IAnalyticsBackend backend;
        private readonly string productId;
        private readonly Func<AnalyticsParameter[]> syncContext;
        private IapState state;
        private Context panel;
        private IapAttempt active, pending;
        private bool panelVisible, disposed;

        public IapTracker(IAnalyticsBackend backend, string productId, IapState initialState,
            Func<AnalyticsParameter[]> syncContext = null)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            this.productId = productId ?? throw new ArgumentNullException(nameof(productId));
            state = initialState ?? throw new ArgumentNullException(nameof(initialState));
            this.syncContext = syncContext;
            // Initial cached ownership is a baseline, never a new purchase event.
        }

        public void EntryClicked(string source, params AnalyticsParameter[] context)
        {
            panel = new Context { Flow = Guid.NewGuid().ToString("N"), Source = source ?? "",
                Price = state.PriceLabel, Extra = Copy(context) };
            panelVisible = false;
            Emit(IapEvents.Actions.EntryClick, "", panel);
        }
        public void PanelShown()
        {
            if (panelVisible) return;
            panelVisible = true;
            Emit(IapEvents.Actions.PanelView, "", panel ?? SynchronizationContext());
        }
        public void PanelClosed()
        {
            if (!panelVisible) return;
            panelVisible = false;
            Emit(IapEvents.Actions.PanelClose, "", panel ?? SynchronizationContext());
            panel = null;
        }

        public IapAttempt BeginPurchase() => Begin(false);
        public IapAttempt BeginRestore() => Begin(true);
        private IapAttempt Begin(bool restore)
        {
            var source = panel ?? SynchronizationContext();
            var context = new Context { Flow = source.Flow, Attempt = Guid.NewGuid().ToString("N"),
                Source = source.Source, Price = state.PriceLabel, Extra = Copy(source.Extra) };
            var attempt = new IapAttempt(this, context, restore, state.IsOwned, active);
            active = attempt;
            Emit(restore ? IapEvents.Actions.RestoreStart : IapEvents.Actions.PurchaseStart, "", context);
            return attempt;
        }

        public void Observe(IapState next)
        {
            if (disposed) return;
            if (next == null) throw new ArgumentNullException(nameof(next));
            var previousOwned = state.IsOwned;
            state = next;
            if (state.IsOwned != previousOwned)
            {
                var context = pending?.Context ?? active?.Context ?? SynchronizationContext();
                Emit(IapEvents.Actions.EntitlementChange, state.IsOwned ? "granted" : "revoked", context, previousOwned);
                if (state.IsOwned) pending?.Complete(IapResult.Succeeded);
            }
            // A completed store query may reveal that a pending purchase no longer exists.
            // Do not invent a decline/cancellation reason, or clear it on a network failure.
            if (!state.IsOwned && state.StoreStatus == "ready") pending?.Complete(IapResult.NotOwned);
        }

        internal void Complete(IapAttempt attempt, IapResult result)
        {
            if (disposed || attempt.Finished || result == IapResult.Pending && attempt.PendingReported) return;
            if (result == IapResult.Pending && !attempt.Restore)
            {
                attempt.PendingReported = true;
                pending = attempt;
            }
            else
            {
                attempt.Finished = true;
                if (pending == attempt) pending = null;
            }
            var value = !attempt.Restore && attempt.InitiallyOwned && result == IapResult.Succeeded
                ? "already_owned" : result == IapResult.NotOwned ? "not_owned" : result.ToString().ToLowerInvariant();
            Emit(attempt.Restore ? IapEvents.Actions.RestoreResult : IapEvents.Actions.PurchaseResult, value, attempt.Context);
            if (active == attempt) active = attempt.Previous;
        }

        private Context SynchronizationContext() => new Context { Price = state.PriceLabel, Extra = Copy(syncContext?.Invoke()) };
        private static AnalyticsParameter[] Copy(AnalyticsParameter[] values) => values == null
            ? Array.Empty<AnalyticsParameter>() : (AnalyticsParameter[])values.Clone();

        private void Emit(string action, string result, Context context, bool? previousOwned = null)
        {
            if (disposed) return;
            try
            {
                var data = new List<AnalyticsParameter>(context.Extra) {
                    AnalyticsParameter.Of(IapEvents.Params.Action, action),
                    AnalyticsParameter.Of(IapEvents.Params.Result, result),
                    AnalyticsParameter.Of(IapEvents.Params.ProductId, productId),
                    AnalyticsParameter.Of(IapEvents.Params.FlowId, context.Flow),
                    AnalyticsParameter.Of(IapEvents.Params.AttemptId, context.Attempt),
                    AnalyticsParameter.Of(IapEvents.Params.Source, context.Source),
                    AnalyticsParameter.Of(IapEvents.Params.PriceLabel, context.Price),
                    AnalyticsParameter.Of(IapEvents.Params.Owned, state.IsOwned ? 1 : 0),
                    AnalyticsParameter.Of(IapEvents.Params.PreviousOwned, (previousOwned ?? state.IsOwned) ? 1 : 0),
                    AnalyticsParameter.Of(IapEvents.Params.StoreStatus, state.StoreStatus)
                };
                backend.LogEvent(IapEvents.Flow, data.ToArray());
            }
            catch (Exception) { /* Diagnostics never control payment or fulfillment. */ }
        }
        public void Dispose() { disposed = true; panel = null; active = null; pending = null; }
    }

    /// <summary>Hold across UI teardown. Pending may be followed by one terminal result; replays are ignored.</summary>
    public sealed class IapAttempt
    {
        private readonly IapTracker tracker;
        internal readonly IapTracker.Context Context;
        internal readonly bool Restore, InitiallyOwned;
        internal readonly IapAttempt Previous;
        internal bool PendingReported, Finished;
        public string AttemptId => Context.Attempt;
        internal IapAttempt(IapTracker tracker, IapTracker.Context context, bool restore, bool owned, IapAttempt previous)
        { this.tracker = tracker; Context = context; Restore = restore; InitiallyOwned = owned; Previous = previous; }
        public void Complete(IapResult result) => tracker.Complete(this, result);
    }
}
