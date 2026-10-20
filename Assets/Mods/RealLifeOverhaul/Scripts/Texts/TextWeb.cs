using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RealLifeOverhaul.Texts
{
    /// <summary>How Your own AI reaches a service: one request, answered or given up within its time. The checks put a pretend one in.</summary>
    public interface ITextWeb
    {
        /// <summary>Sends and waits, on the caller's thread (a worker's, never the game's), at most <paramref name="timeoutMs"/> and a little more.</summary>
        TextWebAnswer Send(TextWebRequest request, int timeoutMs);
    }

    /// <summary>
    /// The web client of Your own AI: .NET's HttpClient (the game ships System.Net.Http with Unity's TLS), one for the
    /// whole session, used only from the runner's worker threads and the sign-in's. Every request has its own time-out
    /// by a cancellation, and the wait for it is bounded again by the caller's own clock (a task that has not ended
    /// <see cref="GraceMs"/> after its time is left to end by itself and given up), so no worker is ever held longer
    /// than its request's time. Nothing of a request or an answer is logged here. Game-free (EconomyCheck,
    /// TextsServiceChecks.cs runs it against a local pretend service).
    /// COST: nothing on the game's thread.
    /// </summary>
    public sealed class TextWeb : ITextWeb
    {
        public const int GraceMs = 500;   // milliseconds past a request's time-out before its wait is given up for certain

        private static readonly HttpClient Client = Make();

        private static HttpClient Make()
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };   // (each request has its own, by its cancellation)
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RealLifeOverhaul/1");
            return client;
        }

        public TextWebAnswer Send(TextWebRequest request, int timeoutMs)
        {
            var answer = new TextWebAnswer();
            var cancel = new CancellationTokenSource(Math.Max(1, timeoutMs));
            try
            {
                Task<TextWebAnswer> task = Run(request, cancel.Token);
                if (!task.Wait(Math.Max(1, timeoutMs) + GraceMs))
                {
                    cancel.Cancel();
                    answer.TimedOut = true;
                    answer.Failure = "no answer within " + Seconds(timeoutMs);
                    return answer;
                }
                return task.Result;
            }
            catch (AggregateException e)
            {
                Exception inner = e.GetBaseException();
                answer.TimedOut = inner is OperationCanceledException || cancel.IsCancellationRequested;
                answer.Failure = answer.TimedOut ? "no answer within " + Seconds(timeoutMs) : "no connection (" + inner.GetType().Name + ")";
                return answer;
            }
            catch (Exception e)
            {
                answer.Failure = "no connection (" + e.GetType().Name + ")";
                return answer;
            }
        }

        private static async Task<TextWebAnswer> Run(TextWebRequest request, CancellationToken token)
        {
            using (var message = new HttpRequestMessage(request.Method == "GET" ? HttpMethod.Get : HttpMethod.Post, request.Url))
            {
                for (int i = 0; i < request.Headers.Count; i++)
                {
                    message.Headers.TryAddWithoutValidation(request.Headers[i].Key, request.Headers[i].Value);
                }
                if (request.Body != null)
                {
                    message.Content = new StringContent(request.Body, Encoding.UTF8);
                    message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType + "; charset=utf-8");
                }
                using (HttpResponseMessage response = await Client.SendAsync(message, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false))
                {
                    var answer = new TextWebAnswer { Status = (int)response.StatusCode };
                    answer.Body = response.Content != null ? await response.Content.ReadAsStringAsync().ConfigureAwait(false) : string.Empty;
                    RetryConditionHeaderValue retry = response.Headers.RetryAfter;
                    if (retry != null && retry.Delta.HasValue)
                    {
                        answer.RetryAfter = retry.Delta.Value.TotalSeconds;
                    }
                    else if (retry != null && retry.Date.HasValue)
                    {
                        answer.RetryAfter = Math.Max(0.0, (retry.Date.Value - DateTimeOffset.UtcNow).TotalSeconds);
                    }
                    return answer;
                }
            }
        }

        private static string Seconds(int ms) => (ms / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s";
    }
}
