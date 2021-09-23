using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace YoutubeTranscriptApi.Tests
{
    // https://github.com/jdepoix/youtube-transcript-api/blob/master/youtube_transcript_api/test/test_api.py

    public class TestYouTubeTranscriptApi : IDisposable
    {
        private readonly TranscriptList _listTranscriptsGJLlxj_dtq8;
        private readonly YouTubeTranscriptApi _youTubeTranscriptApi;
        public TestYouTubeTranscriptApi()
        {
            _youTubeTranscriptApi = new YouTubeTranscriptApi();
            _listTranscriptsGJLlxj_dtq8 = _youTubeTranscriptApi.ListTranscripts("GJLlxj_dtq8");
        }

        public void Dispose()
        {
            _youTubeTranscriptApi.Dispose();
        }

        private string load_asset(string fileName)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "assets", fileName);
            return File.ReadAllText(path);
        }

        [Fact]
        public void test_get_transcript()
        {
            using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
            {
                foreach (var item in youTubeTranscriptApi.GetTranscript("GJLlxj_dtq8"))
                {
                    Trace.WriteLine(JsonSerializer.Serialize(item, new JsonSerializerOptions()
                    {
                        WriteIndented = true
                    }));
                }
            }
        }

        [Fact]
        public void test_list_transcripts()
        {
            //var transcript_list = youTubeTranscriptApi.list_transcripts("GJLlxj_dtq8");
            var transcript_list = _listTranscriptsGJLlxj_dtq8;

            var expected = new[] { "zh", "de", "en", "hi", "ja", "ko", "es", "cs", "en" };
            var language_codes = transcript_list.Select(transcript => transcript.LanguageCode).ToArray();

            Assert.Equal(expected.Length, language_codes.Length);

            foreach (var e in expected)
            {
                Assert.Contains(e, language_codes);
            }
        }

        [Fact]
        public void test_list_transcripts__find_manually_created()
        {
            //var transcript_list = youTubeTranscriptApi.list_transcripts("GJLlxj_dtq8");
            var transcript_list = _listTranscriptsGJLlxj_dtq8;

            var transcript = transcript_list.FindManuallyCreatedTranscript(new[] { "cs" });

            Assert.False(transcript.IsGenerated);
        }

        [Fact]
        public void test_list_transcripts__find_generated()
        {
            //var transcript_list = youTubeTranscriptApi.list_transcripts("GJLlxj_dtq8");
            var transcript_list = _listTranscriptsGJLlxj_dtq8;

            Assert.Throws<NoTranscriptFound>(() => transcript_list.FindGeneratedTranscript(new[] { "cs" }));

            var transcript = transcript_list.FindGeneratedTranscript(new[] { "en" });

            Assert.True(transcript.IsGenerated);
        }

        [Fact]
        public void test_translate_transcript()
        {
            //var transcript = youTubeTranscriptApi.list_transcripts("GJLlxj_dtq8").find_transcript(new[] { "en" });
            var transcript_list = _listTranscriptsGJLlxj_dtq8;
            var transcript = transcript_list.FindTranscript(new[] { "en" });

            var translated_transcript = transcript.Translate("af");

            Assert.Equal("af", translated_transcript.LanguageCode);
            Assert.Contains("&tlang=af", translated_transcript._url);
        }

        [Fact]
        public void test_translate_transcript__translation_language_not_available()
        {
            //var transcript = youTubeTranscriptApi.list_transcripts("GJLlxj_dtq8").find_transcript(new[] { "en" });
            var transcript_list = _listTranscriptsGJLlxj_dtq8;
            var transcript = transcript_list.FindTranscript(new[] { "en" });
            Assert.Throws<TranslationLanguageNotAvailable>(() => transcript.Translate("xyz"));
        }

        [Fact]
        public void test_translate_transcript__not_translatable()
        {
            // changing the test as 'translation_languages' is read-only
            var transcript_list = _listTranscriptsGJLlxj_dtq8;
            var transcript = transcript_list.FindTranscript(new[] { "en" });
            Assert.Throws<TranslationLanguageNotAvailable>(() => transcript.Translate("a123f"));
        }

        [Fact]
        public void test_get_transcript__correct_language_is_used()
        {
            using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
            {
                var transcript = youTubeTranscriptApi.GetTranscript("GJLlxj_dtq8", new[] { "de", "en" }).ToArray();

                // Check that it's in german
                Assert.NotEmpty(transcript);

                Assert.Equal("Hey, wie geht es Dave 2d hier?", transcript[0].Text);

                /*
                query_string = httpretty.last_request().querystring

                self.assertIn('lang', query_string)
                self.assertEqual(len(query_string['lang']), 1)
                self.assertEqual(query_string['lang'][0], 'de')
                    */
            }
        }

        [Fact(Skip = "TO DO")]
        public void test_get_transcript__fallback_language_is_used()
        {

        }

        [Fact(Skip = "TO DO")]
        public void test_get_transcript__create_consent_cookie_if_needed()
        {

            using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
            {
                var transcript = youTubeTranscriptApi.GetTranscript("F1xioXWb8CY").ToList();
            }

            /*
                    httpretty.register_uri(
                    httpretty.GET,
                    'https://www.youtube.com/watch',
                    body=load_asset('youtube_consent_page.html.static')
                )

                YouTubeTranscriptApi.get_transcript('F1xioXWb8CY')
                self.assertEqual(len(httpretty.latest_requests()), 3)
                for request in httpretty.latest_requests()[1:]:
                    self.assertEqual(request.headers['cookie'], 'CONSENT=YES+cb.20210328-17-p0.de+FX+119')
             */

            var html = load_asset("youtube_consent_page.html.static");
            using (var httpHandler = new HttpClientHandler() { CookieContainer = new CookieContainer() })
            using (var httpRequest = new HttpClient(httpHandler))
            {
                var transcriptListFetcher = new TranscriptListFetcher(httpRequest, httpHandler);
                transcriptListFetcher.createConsentCookie(html, "F1xioXWb8CY");
            }
        }

        [Fact]
        public void test_get_transcript__exception_if_create_consent_cookie_failed()
        {
            Assert.Throws<FailedToCreateConsentCookie>(() =>
            {
                string html = load_asset("youtube_consent_page.html.static");
                using (var httpHandler = new HttpClientHandler() { CookieContainer = new CookieContainer() })
                using (var httpRequest = new HttpClient(httpHandler))
                {
                    var transcriptListFetcher = new TranscriptListFetcher(httpRequest, httpHandler);
                    //transcriptListFetcher.extractCaptionsJson(html, "F1xioXWb8CY");
                    transcriptListFetcher.createConsentCookie(html, "F1xioXWb8CY");
                }
            });
        }

        [Fact]
        public void test_get_transcript__exception_if_consent_cookie_age_invalid()
        {
            Assert.Throws<FailedToCreateConsentCookie>(() =>
            {
                string html = load_asset("youtube_consent_page_invalid.html.static");
                using (var httpHandler = new HttpClientHandler() { CookieContainer = new CookieContainer() })
                using (var httpRequest = new HttpClient(httpHandler))
                {
                    var transcriptListFetcher = new TranscriptListFetcher(httpRequest, httpHandler);
                    //transcriptListFetcher.extractCaptionsJson(html, "F1xioXWb8CY");
                    transcriptListFetcher.createConsentCookie(html, "F1xioXWb8CY");
                }
            });
        }

        [Fact]
        public void test_get_transcript__exception_if_video_unavailable()
        {
            Assert.Throws<VideoUnavailable>(() =>
            {
                string html = load_asset("youtube_video_unavailable.html.static");
                using (var httpHandler = new HttpClientHandler() { CookieContainer = new CookieContainer() })
                using (var httpRequest = new HttpClient(httpHandler))
                {
                    var transcriptListFetcher = new TranscriptListFetcher(httpRequest, httpHandler);
                    transcriptListFetcher.extractCaptionsJson(html, "abc");
                }

                /*
                using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
                {
                    youTubeTranscriptApi.get_transcript("abc");
                }
                */
            });
        }

        [Fact]
        public void test_get_transcript__exception_if_youtube_request_limit_reached()
        {
            Assert.Throws<TooManyRequests>(() =>
            {
                string html = load_asset("youtube_too_many_requests.html.static");
                using (var httpHandler = new HttpClientHandler() { CookieContainer = new CookieContainer() })
                using (var httpRequest = new HttpClient(httpHandler))
                {
                    var transcriptListFetcher = new TranscriptListFetcher(httpRequest, httpHandler);
                    transcriptListFetcher.extractCaptionsJson(html, "abc");
                }
            });
        }

        [Fact]
        public void test_get_transcript__exception_if_transcripts_disabled()
        {
            Assert.Throws<TranscriptsDisabled>(() =>
            {
                using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
                {
                    youTubeTranscriptApi.GetTranscript("dsMFmonKDD4");
                }
            });
        }

        [Fact]
        public void test_get_transcript__exception_if_language_unavailable()
        {
            Assert.Throws<NoTranscriptFound>(() =>
            {
                using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
                {
                    youTubeTranscriptApi.GetTranscript("GJLlxj_dtq8", languages: new[] { "cz" });
                }
            });
        }

        [Fact]
        public void test_get_transcript__exception_if_no_transcript_available()
        {
            Assert.Throws<NoTranscriptAvailable>(() =>
            {
                string html = load_asset("youtube_no_transcript_available.html.static");
                using (var httpHandler = new HttpClientHandler() { CookieContainer = new CookieContainer() })
                using (var httpRequest = new HttpClient(httpHandler))
                {
                    var transcriptListFetcher = new TranscriptListFetcher(httpRequest, httpHandler);
                    transcriptListFetcher.extractCaptionsJson(html, "MwBPvcYFY2E");
                }

                using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
                {
                    youTubeTranscriptApi.GetTranscript("MwBPvcYFY2E");
                }
            });
        }

        [Fact(Skip = "TO DO - Proxy not implemented")]
        public void test_get_transcript__with_proxy()
        { }

        [Fact]
        public void test_get_transcript__with_cookies()
        {
            using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
            {
                var transcript = youTubeTranscriptApi.GetTranscript("GJLlxj_dtq8", cookies: Path.Combine(Directory.GetCurrentDirectory(), "example_cookies.txt"));
            }
        }

        // TO FINSIH

        [Fact]
        public void test_load_cookies()
        {
            var dirname = Directory.GetCurrentDirectory();
            var cookies = Path.Combine(dirname, "example_cookies.txt");

            using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
            {
                var session_cookies = youTubeTranscriptApi.loadCookies(cookies, "GJLlxj_dtq8");

                Assert.Single(session_cookies);
                Assert.Equal("TEST_FIELD=TEST_VALUE", session_cookies[0].ToString());
            }
        }

        [Fact]
        public void test_load_cookies__bad_file_path()
        {
            Assert.Throws<CookiePathInvalid>(() =>
            {
                var cookies = "nonexistent_cookies.txt";

                using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
                {
                    var session_cookies = youTubeTranscriptApi.loadCookies(cookies, "GJLlxj_dtq8");
                }
            });
        }

        [Fact]
        public void test_load_cookies__no_valid_cookies()
        {
            Assert.Throws<CookiesInvalid>(() =>
            {
                var dirname = Directory.GetCurrentDirectory();
                var cookies = Path.Combine(dirname, "expired_example_cookies.txt");

                using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
                {
                    var session_cookies = youTubeTranscriptApi.loadCookies(cookies, "GJLlxj_dtq8");
                }
            });
        }
    }
}
