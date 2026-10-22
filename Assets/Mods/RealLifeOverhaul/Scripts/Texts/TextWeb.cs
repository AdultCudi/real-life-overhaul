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
        /// <summary>Sends and waits on the caller's thread, never the game's, at most <paramref name="timeoutMs"/> and a little more.</summary>
        TextWebAnswer Send(TextWebRequest request, int timeoutMs);
    }

    /// <summary>
    /// The web client of Your own AI: one HttpClient for the session, used only from worker threads. Every request has its
    /// own time-out, and the wait is bounded again by the caller's clock, so no worker is held past its request's time. A
    /// streamed answer is read line by line and left as soon as its text is whole. Nothing is logged here. Game-free.
    /// COST: nothing on the game's thread.
    /// </summary>
    public sealed class TextWeb : ITextWeb
    {
        public const int GraceMs = 500;   // milliseconds past a request's time-out before its wait is given up for certain

        private static readonly HttpClient Client = Make();

        private static HttpClient Make()
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };   // each request has its own, by its cancellation
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
                bool streamed = request.Lines != null;
                HttpCompletionOption until = streamed ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
                using (HttpResponseMessage response = await Client.SendAsync(message, until, token).ConfigureAwait(false))
                {
                    var answer = new TextWebAnswer { Status = (int)response.StatusCode };
                    if (response.Content == null)
                    {
                        answer.Body = string.Empty;
                    }
                    else if (streamed && answer.Status >= 200 && answer.Status <= 299)
                    {
                        using (token.Register(response.Dispose))   // the time-out ends a read that waits on the network
                        {
                            answer.Body = await ReadLines(response.Content, request.Lines).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        answer.Body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }
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

        // A streamed answer's lines, each told to the reader until it says the answer is whole or the stream ends: the body
        // read so far, so the line is shown at its last word rather than when the service closes the answer.
        private static async Task<string> ReadLines(HttpContent content, Func<string, bool> lines)
        {
            var body = new StringBuilder(2048);
            using (System.IO.Stream stream = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var reader = new System.IO.StreamReader(stream, Encoding.UTF8))
            {
                string line;
                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    body.Append(line).Append('\n');
                    if (lines(line))
                    {
                        break;
                    }
                }
            }
            return body.ToString();
        }

        private static string Seconds(int ms) => (ms / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s";
    }
}
