using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Tracking.Ads
{
    /// <summary>SDK-independent ad tracking. All lifecycle objects are used on the host's callback thread.</summary>
    public sealed class AdTracker
    {
        private readonly IAnalyticsBackend backend;
        private readonly Func<double> now;
        public AdTracker(IAnalyticsBackend backend, Func<double> monotonicSeconds = null)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            now = monotonicSeconds ?? (() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
        }

        public AdFlow Opportunity(string format, string placement, params AnalyticsParameter[] context)
        {
            var flow = new AdFlow(this, format, placement, context);
            flow.Emit(AdEvents.Opportunity);
            return flow;
        }

        public AdLoad Request(string format, string unit)
        {
            var load = new AdLoad(this, format, unit);
            load.EmitRequest();
            return load;
        }

        /// <summary>SDK-managed refresh has no observable request start. No request id or elapsed time is invented.</summary>
        public void AutomaticFill(string format, string unit, bool success, params AnalyticsParameter[] details)
        {
            Emit(AdEvents.Fill, new[] {
                AnalyticsParameter.Of(AdEvents.Params.Format, format),
                AnalyticsParameter.Of(AdEvents.Params.Unit, unit),
                AnalyticsParameter.Of(AdEvents.Params.LoadOrigin, "sdk_auto"),
                AnalyticsParameter.Of(AdEvents.Params.Result, success ? AdEvents.Results.Success : AdEvents.Results.Failure)
            }, details);
        }

        /// <summary>Revenue callback only. Zero is valid; negative/NaN/infinite revenue is not.</summary>
        public void Impression(string platform, string unit, string format, string placement, string source,
            double value, string currency, params AnalyticsParameter[] context)
        {
            if (value < 0 || double.IsNaN(value) || double.IsInfinity(value)) return;
            Emit(AdEvents.Impression, context,
                AnalyticsParameter.Of(AdEvents.Params.Platform, platform),
                AnalyticsParameter.Of(AdEvents.Params.Unit, unit ?? ""),
                AnalyticsParameter.Of(AdEvents.Params.Format, format ?? ""),
                AnalyticsParameter.Of(AdEvents.Params.Placement, placement ?? ""),
                AnalyticsParameter.Of(AdEvents.Params.Source, source ?? ""),
                AnalyticsParameter.Of(AdEvents.Params.Value, value),
                AnalyticsParameter.Of(AdEvents.Params.Currency, currency));
        }

        public void Click(string format, string placement, params AnalyticsParameter[] details)
        {
            Emit(AdEvents.Clicked, new[] {
                AnalyticsParameter.Of(AdEvents.Params.Format, format),
                AnalyticsParameter.Of(AdEvents.Params.Placement, placement)
            }, details);
        }

        internal double Now => now();
        internal long Elapsed(double start) => (long)Math.Max(0, (Now - start) * 1000);
        internal void Emit(string name, AnalyticsParameter[] context, params AnalyticsParameter[] details)
        {
            // Later standard fields win over conflicting host extras; never emit duplicate parameter keys.
            var parameters = new List<AnalyticsParameter>();
            foreach (var group in new[] {context, details})
            {
                if (group == null) continue;
                foreach (var parameter in group)
                {
                    var index = parameters.FindIndex(p => p.Name == parameter.Name);
                    if (index < 0) parameters.Add(parameter); else parameters[index] = parameter;
                }
            }
            backend.LogEvent(name, parameters.ToArray());
        }
    }

    public sealed class AdLoad
    {
        private readonly AdTracker tracker;
        private readonly AnalyticsParameter[] context;
        private readonly double started;
        private bool filled;
        public string RequestId { get; } = Guid.NewGuid().ToString("N");
        internal AdLoad(AdTracker tracker, string format, string unit)
        {
            this.tracker = tracker;
            started = tracker.Now;
            context = new[] {
                AnalyticsParameter.Of(AdEvents.Params.RequestId, RequestId),
                AnalyticsParameter.Of(AdEvents.Params.Format, format),
                AnalyticsParameter.Of(AdEvents.Params.Unit, unit),
                AnalyticsParameter.Of(AdEvents.Params.LoadOrigin, "explicit")
            };
        }
        internal void EmitRequest() => tracker.Emit(AdEvents.Request, context);
        public void Fill(bool success, params AnalyticsParameter[] details)
        {
            if (filled) return;
            filled = true;
            var data = new List<AnalyticsParameter>(details ?? Array.Empty<AnalyticsParameter>()) {
                AnalyticsParameter.Of(AdEvents.Params.Result, success ? AdEvents.Results.Success : AdEvents.Results.Failure),
                AnalyticsParameter.Of(AdEvents.Params.DurationMs, tracker.Elapsed(started))
            };
            tracker.Emit(AdEvents.Fill, context, data.ToArray());
        }
    }

    /// <summary>One business opportunity. Context is copied before asynchronous SDK callbacks; results are deduplicated.</summary>
    public sealed class AdFlow
    {
        private readonly AdTracker tracker;
        private readonly AnalyticsParameter[] context;
        private bool decided, requested, resulted, closed, earned, rewarded;
        private double requestTime, displayTime;
        private bool displayed;
        public string FlowId { get; } = Guid.NewGuid().ToString("N");
        internal AdFlow(AdTracker tracker, string format, string placement, AnalyticsParameter[] extra)
        {
            this.tracker = tracker;
            var data = new List<AnalyticsParameter>(extra ?? Array.Empty<AnalyticsParameter>()) {
                AnalyticsParameter.Of(AdEvents.Params.FlowId, FlowId),
                AnalyticsParameter.Of(AdEvents.Params.Format, format),
                AnalyticsParameter.Of(AdEvents.Params.Placement, placement)
            };
            context = data.ToArray();
        }
        internal void Emit(string name, params AnalyticsParameter[] details) => tracker.Emit(name, context, details);
        public void Decision(string result, string reason)
        {
            if (decided) return;
            decided = true;
            Emit(AdEvents.Decision, AnalyticsParameter.Of(AdEvents.Params.Result, result), AnalyticsParameter.Of(AdEvents.Params.Reason, reason));
        }
        public void ShowRequest(string requestId = null)
        {
            if (requested) return;
            requested = true;
            requestTime = tracker.Now;
            Emit(AdEvents.ShowRequest, AnalyticsParameter.Of(AdEvents.Params.RequestId, requestId ?? ""));
        }
        public void ShowResult(bool success, params AnalyticsParameter[] details)
        {
            if (!requested || resulted) return;
            resulted = true;
            displayed = success;
            displayTime = tracker.Now;
            var data = new List<AnalyticsParameter>(details ?? Array.Empty<AnalyticsParameter>()) {
                AnalyticsParameter.Of(AdEvents.Params.Result, success ? AdEvents.Results.Success : AdEvents.Results.Failure),
                AnalyticsParameter.Of(AdEvents.Params.DurationMs, tracker.Elapsed(requestTime))
            };
            Emit(AdEvents.ShowResult, data.ToArray());
        }
        public void Click(params AnalyticsParameter[] details) => Emit(AdEvents.Clicked, details);

        public void Close(params AnalyticsParameter[] details)
        {
            if (!requested || closed) return;
            closed = true;
            var data = new List<AnalyticsParameter>(details ?? Array.Empty<AnalyticsParameter>()) {
                AnalyticsParameter.Of(AdEvents.Params.RewardEarned, earned ? 1L : 0L)
            };
            if (displayed) data.Add(AnalyticsParameter.Of(AdEvents.Params.DurationMs, tracker.Elapsed(displayTime)));
            Emit(AdEvents.Closed, data.ToArray());
        }
        public void RewardEarned()
        {
            if (earned) return;
            earned = true;
            Emit(AdEvents.RewardEarned);
        }
        public void RewardResult(bool granted, string reason)
        {
            if (!earned || rewarded) return;
            rewarded = true;
            Emit(AdEvents.RewardResult,
                AnalyticsParameter.Of(AdEvents.Params.Result, granted ? AdEvents.Results.Granted : AdEvents.Results.NotGranted),
                AnalyticsParameter.Of(AdEvents.Params.Reason, reason));
        }
    }
}
