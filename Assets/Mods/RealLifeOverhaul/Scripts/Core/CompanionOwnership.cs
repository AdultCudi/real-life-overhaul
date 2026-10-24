using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Bounded, read-only ownership requests. Every target is validated before any owner is queried. Runs only on a
    /// reader's query, nothing per frame; a fault answers "unknown" and is logged once.
    /// </summary>
    internal static class CompanionOwnership
    {
        /// <summary>
        /// The answer to one ownership request, as JSON; a malformed request or a fault answers "unknown". Read only
        /// during an ownership query, nothing per frame.
        /// </summary>
        public static string Query(GameInstance game, string json)
        {
            try
            {
                if (!CompanionGame.Available(game) || string.IsNullOrEmpty(json)
                    || Encoding.UTF8.GetByteCount(json) > 262144)
                {
                    return Result("unknown", "rlo", "session", "ownership-unavailable");
                }
                JObject request;
                using (var input = new JsonTextReader(new StringReader(json)))
                {
                    input.MaxDepth = 16;
                    input.DateParseHandling = DateParseHandling.None;
                    request = JObject.Load(input, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (input.Read())
                    {
                        throw new ArgumentException("Trailing ownership input");
                    }
                }
                if (!Integer(request["schemaVersion"], 1, 1) || !Word(request["operation"]))
                {
                    throw new ArgumentException("Invalid ownership request");
                }
                string operation = (string)request["operation"], phase = (string)request["phase"];
                if ((operation != "schedule" && operation != "hire" && operation != "wholesale" && operation != "rent")
                    || (phase != "review" && phase != "apply" && phase != "undo")
                    || !(request["targets"] is JArray targets) || targets.Count < 1 || targets.Count > 256)
                {
                    throw new ArgumentException("Invalid ownership operation");
                }
                foreach (JToken target in targets)
                {
                    Validate(target as JObject, operation);
                }
                if (CompanionBridges.Ownership == null)
                {
                    return Result("unknown", "rlo", operation, "ownership-provider-unavailable");
                }
                return CompanionSnapshot.Json(CompanionBridges.Ownership(operation, targets));
            }
            catch (Exception e)
            {
                CompanionGame.Failed("ownership", e);
                return Result("unknown", "rlo", "request", "ownership-invalid-or-failed");
            }
        }

        /// <summary>One decision in the response's shape, with at most one reason and one effect.</summary>
        internal static object Decision(string decision, string owner, string task, string reason, string effect = null)
        {
            return CompanionSnapshot.Row("schemaVersion", 1, "decision", decision, "owner", owner, "task", task,
                "reasons", reason == null ? new string[0] : new[] { reason },
                "effects", effect == null ? new string[0] : new[] { effect });
        }

        private static string Result(string decision, string owner, string task, string reason)
        {
            return CompanionSnapshot.Json(Decision(decision, owner, task, reason));
        }

        private static bool Word(JToken token)
        {
            return token != null && token.Type == JTokenType.String && ((string)token).Length > 0
                && ((string)token).Length <= 1024;
        }

        private static bool Integer(JToken token, int min, int max)
        {
            return token != null && token.Type == JTokenType.Integer && (long)token >= min && (long)token <= max;
        }

        private static void Validate(JObject target, string operation)
        {
            if (target == null || !Word(target["street"]) || !Integer(target["number"], 0, 1000000))
            {
                throw new ArgumentException("Invalid target address");
            }
            if (operation == "hire")
            {
                foreach (string flag in new[] { "hiring", "moving", "scheduling" })
                {
                    if (target[flag]?.Type != JTokenType.Boolean)
                    {
                        throw new ArgumentException("Missing hire operation");
                    }
                }
                if (!(bool)target["hiring"] && !(bool)target["moving"] && !(bool)target["scheduling"])
                {
                    throw new ArgumentException("Empty hire operation");
                }
            }
            if (operation == "schedule" || (operation == "hire" && (bool)target["scheduling"]))
            {
                Week(target["week"] as JArray);
            }
            if (operation == "wholesale")
            {
                string action = (string)target["action"];
                if (!Word(target["contractId"]) || (action != "set" && action != "remove")
                    || !(target["itemIds"] is JArray items) || items.Count > 1000
                    || (items.Count == 0 && action != "remove"))
                {
                    throw new ArgumentException("Invalid wholesale target");
                }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (JToken item in items)
                {
                    if (!Word(item) || !seen.Add((string)item))
                    {
                        throw new ArgumentException("Invalid wholesale item");
                    }
                }
            }
        }

        private static void Week(JArray week)
        {
            if (week == null || week.Count != 7)
            {
                throw new ArgumentException("A full week is required");
            }
            int seen = 0;
            foreach (JToken day in week)
            {
                if (!(day is JObject) || !Integer(day["day"], 0, 6) || day["isOpen"]?.Type != JTokenType.Boolean
                    || !(day["hours"] is JArray hours) || !(day["shifts"] is JArray shifts)
                    || hours.Count > 24 || shifts.Count > 1000)
                {
                    throw new ArgumentException("Invalid week day");
                }
                int bit = 1 << (int)day["day"];
                if ((seen & bit) != 0 || (!(bool)day["isOpen"] && hours.Count != 0))
                {
                    throw new ArgumentException("Duplicate or closed day");
                }
                seen |= bit;
                foreach (JToken hour in hours)
                {
                    Span(hour);
                }
                foreach (JToken shift in shifts)
                {
                    Span(shift);
                    if (!Word(shift["employeeId"]) || (shift["itemInstanceId"]?.Type != JTokenType.Null
                        && !Word(shift["itemInstanceId"])))
                    {
                        throw new ArgumentException("Invalid employee assignment");
                    }
                }
            }
        }

        private static void Span(JToken span)
        {
            if (!(span is JObject) || !Integer(span["from"], 0, 24) || !Integer(span["to"], 0, 24)
                || (int)span["from"] > (int)span["to"])
            {
                throw new ArgumentException("Invalid hours");
            }
        }
    }
}
