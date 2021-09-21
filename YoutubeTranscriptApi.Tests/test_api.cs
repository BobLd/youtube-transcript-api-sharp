using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace YoutubeTranscriptApi.Tests
{
    // https://github.com/jdepoix/youtube-transcript-api/blob/master/youtube_transcript_api/test/test_api.py

    public class TestYouTubeTranscriptApi
    {
        private readonly TranscriptList list_transcripts_GJLlxj_dtq8;

        public TestYouTubeTranscriptApi()
        {
            var youTubeTranscriptApi = new YouTubeTranscriptApi();
            list_transcripts_GJLlxj_dtq8 = youTubeTranscriptApi.list_transcripts("GJLlxj_dtq8");
        }

        [Fact]
        public void test_get_transcript()
        {
            using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
            {
                var transcript = youTubeTranscriptApi.get_transcript("GJLlxj_dtq8");

                foreach (var item in transcript)
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
            var transcript_list = list_transcripts_GJLlxj_dtq8;

            var language_codes = transcript_list.Select(transcript => transcript.language_code).ToArray();

            Assert.Equal(new[] { "zh", "de", "en", "hi", "ja", "ko", "es", "cs", "en" }, language_codes);
        }

        [Fact]
        public void test_list_transcripts__find_manually_created()
        {
            //var transcript_list = youTubeTranscriptApi.list_transcripts("GJLlxj_dtq8");
            var transcript_list = list_transcripts_GJLlxj_dtq8;

            var transcript = transcript_list.find_manually_created_transcript(new[] { "cs" });

            Assert.False(transcript.is_generated);
        }

        [Fact]
        public void test_list_transcripts__find_generated()
        {
            //var transcript_list = youTubeTranscriptApi.list_transcripts("GJLlxj_dtq8");
            var transcript_list = list_transcripts_GJLlxj_dtq8;

            Assert.Throws<NoTranscriptFound>(() => transcript_list.find_generated_transcript(new[] { "cs" }));

            var transcript = transcript_list.find_generated_transcript(new[] { "en" });

            Assert.True(transcript.is_generated);
        }

        [Fact]
        public void test_translate_transcript()
        {
            //var transcript = youTubeTranscriptApi.list_transcripts("GJLlxj_dtq8").find_transcript(new[] { "en" });
            var transcript_list = list_transcripts_GJLlxj_dtq8;
            var transcript = transcript_list.find_transcript(new[] { "en" });

            var translated_transcript = transcript.translate("af");

            Assert.Equal("af", translated_transcript.language_code);
            Assert.Contains("&tlang=af", translated_transcript._url);
        }

        [Fact]
        public void test_translate_transcript__translation_language_not_available()
        {
            //var transcript = youTubeTranscriptApi.list_transcripts("GJLlxj_dtq8").find_transcript(new[] { "en" });
            var transcript_list = list_transcripts_GJLlxj_dtq8;
            var transcript = transcript_list.find_transcript(new[] { "en" });
            Assert.Throws<TranslationLanguageNotAvailable>(() => transcript.translate("xyz"));
        }

        [Fact(Skip = "read-only for the moment")]
        public void test_translate_transcript__not_translatable()
        {
            //var transcript = youTubeTranscriptApi.list_transcripts("GJLlxj_dtq8").find_transcript(new[] { "en" });
            //var transcript_list = list_transcripts_GJLlxj_dtq8;
            //var transcript = transcript_list.find_transcript(new[] { "en" });
            //transcript.translation_languages = [];
            //with self.assertRaises(NotTranslatable):
            //    transcript.translate('af')
        }

        [Fact(Skip = "TO DO")]
        public void test_get_transcript__correct_language_is_used()
        {
            /*
            using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
            {
                youTubeTranscriptApi.get_transcript('GJLlxj_dtq8', ['de', 'en'])
                query_string = httpretty.last_request().querystring

                self.assertIn('lang', query_string)
                self.assertEqual(len(query_string['lang']), 1)
                self.assertEqual(query_string['lang'][0], 'de')
            }
            */
        }

        [Fact(Skip = "TO DO")]
        public void test_get_transcript__fallback_language_is_used()
        {

        }

        [Fact(Skip = "TO DO")]
        public void test_get_transcript__create_consent_cookie_if_needed()
        {

        }

        [Fact(Skip = "TO DO")]
        public void test_get_transcript__exception_if_create_consent_cookie_failed()
        {

        }

        [Fact(Skip = "TO DO")]
        public void test_get_transcript__exception_if_consent_cookie_age_invalid()
        {

        }

        [Fact(Skip = "TO DO")]
        public void test_get_transcript__exception_if_video_unavailable()
        {

        }

        [Fact(Skip = "TO DO")]
        public void test_get_transcript__exception_if_youtube_request_limit_reached()
        {

        }

        [Fact(Skip = "TO DO")]
        public void test_get_transcript__exception_if_transcripts_disabled()
        {

        }

        [Fact]
        public void test_get_transcript__exception_if_language_unavailable()
        {
            Assert.Throws<NoTranscriptFound>(() =>
            {
                using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
                {
                    youTubeTranscriptApi.get_transcript("GJLlxj_dtq8", languages: new[] { "cz" });
                }
            });
        }

        [Fact(Skip = "TO DO")]
        public void test_get_transcript__exception_if_no_transcript_available()
        {

        }

        [Fact(Skip = "TO DO")]
        public   void test_get_transcript__with_proxy()
        {

        }

        [Fact(Skip = "TO DO")]
        public void test_get_transcript__with_cookies()
        {

        }

        // TO FINSIH
    }
}
