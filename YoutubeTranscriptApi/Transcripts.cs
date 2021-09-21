using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace YoutubeTranscriptApi
{
    //https://github.com/jdepoix/youtube-transcript-api/blob/c5bf0132ffa2906cc1bf6d480a70ef799dedc209/youtube_transcript_api/_transcripts.py

    internal class TranscriptListFetcher
    {
        private readonly HttpClient _http_client;

        /// <summary>
        /// Initializes a new instance of the <see cref="TranscriptListFetcher"/> class.
        /// </summary>
        /// <param name="http_client"></param>
        public TranscriptListFetcher(HttpClient http_client)
        {
            _http_client = http_client;
        }

        public TranscriptList fetch(string video_id)
        {
            return TranscriptList.build(
                _http_client,
                video_id,
                _extract_captions_json(_fetch_video_html(video_id), video_id));
        }

        public JsonElement _extract_captions_json(string html, string video_id)
        {
            var splitted_html = html.Split("\"captions\":");

            if (splitted_html.Length <= 1)
            {
                if (html.Contains("class=\"g-recaptcha\""))
                {
                    throw new TooManyRequests(video_id);
                }

                if (html.Contains("\"playabilityStatus\":"))
                {
                    throw new VideoUnavailable(video_id);
                }

                throw new TranscriptsDisabled(video_id);
            }

            var captions_json = JsonSerializer.Deserialize<JsonElement>(
                splitted_html[1].Split(",\"videoDetails")[0].Replace("\n", "")
                ).GetProperty("playerCaptionsTracklistRenderer");

            if (!captions_json.TryGetProperty("captionTracks", out _))
            {
                throw new NoTranscriptAvailable(video_id);
            }

            return captions_json;
        }

        public void _create_consent_cookie(string html, string video_id)
        {
            var match = Regex.Match("name=\"v\" value=\"(.*?)\"", html);
            if (!match.Success)
            {
                throw new FailedToCreateConsentCookie(video_id);
            }
            //this._http_client.cookies.set('CONSENT', 'YES+' + match.group(1), domain = '.youtube.com')

            throw new NotImplementedException();
        }

        public string _fetch_video_html(string video_id)
        {
            var html = this._fetch_html(video_id);
            if (html.Contains("action=\"https://consent.youtube.com/s\""))
            {
                this._create_consent_cookie(html, video_id);
                html = this._fetch_html(video_id);
                if (html.Contains("action=\"https://consent.youtube.com/s\""))
                {
                    throw new FailedToCreateConsentCookie(video_id);
                }
            }
            return html;
        }

        public string _fetch_html(string video_id)
        {
            string debug_url = string.Format(Settings.WATCH_URL, video_id);
            var raw_result = _http_client.GetStringAsync(debug_url).Result;
            var result = raw_result.Replace(
                @"\u0026", "&"
                ).Replace(
                "\\", ""
                );
            return result;
        }
    }

    /// <summary>
    /// This object represents a list of transcripts. It can be iterated over to list all transcripts which are available
    /// for a given YouTube video. Also it provides functionality to search for a transcript in a given language.
    /// </summary>
    public class TranscriptList : IEnumerable<Transcript>
    {
        public string video_id { get; }

        private readonly Dictionary<string, Transcript> _manually_created_transcripts;
        private readonly Dictionary<string, Transcript> _generated_transcripts;
        private readonly List<Dictionary<string, string>> _translation_languages;

        /// <summary>
        /// The constructor is only for internal use. Use the static build method instead.
        /// </summary>
        /// <param name="video_id"> the id of the video this TranscriptList is for</param>
        /// <param name="manually_created_transcripts">dict mapping language codes to the manually created transcripts</param>
        /// <param name="generated_transcripts">dict mapping language codes to the generated transcripts</param>
        /// <param name="translation_languages">list of languages which can be used for translatable languages</param>
        internal TranscriptList(string video_id, Dictionary<string, Transcript> manually_created_transcripts, Dictionary<string, Transcript> generated_transcripts, List<Dictionary<string, string>> translation_languages)
        {
            this.video_id = video_id;
            this._manually_created_transcripts = manually_created_transcripts;
            this._generated_transcripts = generated_transcripts;
            this._translation_languages = translation_languages;
        }

        /// <summary>
        /// Factory method for TranscriptList.
        /// </summary>
        /// <param name="http_client">http client which is used to make the transcript retrieving http calls</param>
        /// <param name="video_id">the id of the video this TranscriptList is for</param>
        /// <param name="captions_json">the JSON parsed from the YouTube pages static HTML</param>
        /// <returns>the created <see cref="TranscriptList"/></returns>
        public static TranscriptList build(HttpClient http_client, string video_id, JsonElement captions_json)
        {
            var translation_languages = new List<Dictionary<string, string>>();
            foreach (var translation_language in captions_json.GetProperty("translationLanguages").EnumerateArray())
            {
                translation_languages.Add(new Dictionary<string, string>
                {
                    { "language", translation_language.GetProperty("languageName").GetProperty("simpleText").GetString() },
                    { "language_code", translation_language.GetProperty("languageCode").GetString() }
                });
            }

            var manually_created_transcripts = new Dictionary<string, Transcript>();
            var generated_transcripts = new Dictionary<string, Transcript>();

            foreach (var caption in captions_json.GetProperty("captionTracks").EnumerateArray())
            {
                string kind = string.Empty;
                if (caption.TryGetProperty("kind", out var kindJson))
                {
                    kind = kindJson.GetString();
                }

                bool isTranslatable = false;
                if (caption.TryGetProperty("isTranslatable", out var isTranslatableJson))
                {
                    isTranslatable = isTranslatableJson.GetBoolean();
                }

                var transcript = new Transcript(
                    http_client,
                    video_id,
                    caption.GetProperty("baseUrl").GetString(),
                    caption.GetProperty("name").GetProperty("simpleText").GetString(),
                    caption.GetProperty("languageCode").GetString(),
                    kind == "asr",
                    isTranslatable ? translation_languages : new List<Dictionary<string, string>>());

                if (kind == "asr")
                {
                    generated_transcripts.Add(caption.GetProperty("languageCode").GetString(), transcript);
                }
                else
                {
                    manually_created_transcripts.Add(caption.GetProperty("languageCode").GetString(), transcript);
                }
            }

            return new TranscriptList(
                video_id,
                manually_created_transcripts,
                generated_transcripts,
                translation_languages
            );
        }

        /// <inheritdoc/>
        public IEnumerator<Transcript> GetEnumerator()
        {
            foreach (var val in _manually_created_transcripts.Values.Concat(_generated_transcripts.Values))
            {
                yield return val;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Finds a transcript for a given language code. Manually created transcripts are returned first and only if none
        /// are found, generated transcripts are used. If you only want generated transcripts use
        /// find_manually_created_transcript instead.
        /// </summary>
        /// <param name="language_codes">A list of language codes in a descending priority. For example, if this is set to
        /// ['de', 'en'] it will first try to fetch the german transcript(de) and then fetch the english transcript(en) if
        /// it fails to do so.</param>
        /// <returns>the found Transcript</returns>
        public Transcript find_transcript(IReadOnlyList<string> language_codes)
        {
            return _find_transcript(language_codes, new[]
            {
                _manually_created_transcripts,
                _generated_transcripts
            });
        }

        /// <summary>
        /// Finds a automatically generated transcript for a given language code.
        /// </summary>
        /// <param name="language_codes">A list of language codes in a descending priority. For example, if this is set to
        /// ['de', 'en'] it will first try to fetch the german transcript(de) and then fetch the english transcript(en) if
        /// it fails to do so.</param>
        /// <returns>the found Transcript</returns>
        public Transcript find_generated_transcript(IReadOnlyList<string> language_codes)
        {
            return _find_transcript(language_codes, new[] { _generated_transcripts });
        }

        /// <summary>
        /// Finds a manually created transcript for a given language code.
        /// </summary>
        /// <param name="language_codes">A list of language codes in a descending priority. For example, if this is set to
        /// ['de', 'en'] it will first try to fetch the german transcript(de) and then fetch the english transcript(en) if
        /// it fails to do so.</param>
        /// <returns>the found Transcript</returns>
        public Transcript find_manually_created_transcript(IReadOnlyList<string> language_codes)
        {
            return _find_transcript(language_codes, new[] { _manually_created_transcripts });
        }

        private Transcript _find_transcript(IReadOnlyList<string> language_codes, IReadOnlyList<Dictionary<string, Transcript>> transcript_dicts)
        {
            foreach (var language_code in language_codes)
            {
                foreach (var transcript_dict in transcript_dicts)
                {
                    if (transcript_dict.TryGetValue(language_code, out var val))
                    {
                        return val;
                    }
                }
            }

            throw new NoTranscriptFound(this.video_id, language_codes, this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"For this video ({video_id}) transcripts are available in the following languages:\n\n" +
                "(MANUALLY CREATED)\n" +
                $"{_get_language_description(_manually_created_transcripts.Values.Select(transcript => transcript.ToString()))}\n\n" +
                "(GENERATED)\n" +
                $"{_get_language_description(_generated_transcripts.Values.Select(transcript => transcript.ToString()))}\n\n" +
                "(TRANSLATION LANGUAGES)\n" +
                $"{_get_language_description(_translation_languages.Select(translation_language => $"{ translation_language["language_code"]} (\"{translation_language["language"]}\")"))}";
        }

        private string _get_language_description(IEnumerable<string> transcript_strings)
        {
            if (!transcript_strings.Any()) return "None";
            return string.Join("\n", transcript_strings.Select(transcript => $" - {transcript}"));
        }
    }

    public class Transcript
    {
        private readonly HttpClient _http_client;
        public readonly string _url;

        public string video_id { get; }
        public string language { get; }
        public string language_code { get; }
        public bool is_generated { get; }

        private readonly IReadOnlyList<Dictionary<string, string>> translation_languages;
        private readonly Dictionary<string, string> _translation_languages_dict;

        /// <summary>
        /// You probably don't want to initialize this directly. Usually you'll access Transcript objects using a
        /// TranscriptList.
        /// </summary>
        /// <param name="http_client">http client which is used to make the transcript retrieving http calls</param>
        /// <param name="video_id">the id of the video this TranscriptList is for</param>
        /// <param name="url">the url which needs to be called to fetch the transcript</param>
        /// <param name="language">the name of the language this transcript uses</param>
        /// <param name="language_code"></param>
        /// <param name="is_generated"></param>
        /// <param name="translation_languages"></param>
        public Transcript(HttpClient http_client, string video_id, string url, string language, string language_code,
            bool is_generated, IReadOnlyList<Dictionary<string, string>> translation_languages)
        {
            this._http_client = http_client;
            this.video_id = video_id;
            this._url = url;
            this.language = language;
            this.language_code = language_code;
            this.is_generated = is_generated;
            this.translation_languages = translation_languages;
            this._translation_languages_dict = new Dictionary<string, string>();
            foreach (var translation_language in translation_languages)
            {
                _translation_languages_dict.Add(translation_language["language_code"], translation_language["language"]);
            }
        }

        /// <summary>
        /// Loads the actual transcript data.
        /// </summary>
        /// <returns>a list of <see cref="TranscriptItem"/> containing the 'text', 'start' and 'duration' keys</returns>
        public IEnumerable<TranscriptItem> fetch()
        {
            return new _TranscriptParser().parse(this._http_client.GetStringAsync(this._url).Result);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{language_code} (\"{language}\"){(is_translatable ? "[TRANSLATABLE]" : "")}";
        }

        public bool is_translatable => translation_languages.Count() > 0;

        public Transcript translate(string language_code)
        {
            if (!is_translatable)
            {
                throw new NotTranslatable(video_id);
            }

            if (!_translation_languages_dict.ContainsKey(language_code))
            {
                throw new TranslationLanguageNotAvailable(video_id);
            }

            return new Transcript(
                _http_client,
                video_id,
                $"{_url}&tlang={language_code}",
                _translation_languages_dict[language_code],
                language_code,
                true,
                Array.Empty<Dictionary<string, string>>()
            );
        }
    }

    public struct TranscriptItem
    {
        public string Text { get; init; }

        public float Start { get; init; }

        public float Duration { get; init; }
    }

    internal class _TranscriptParser
    {
        private readonly Regex HTML_TAG_REGEX = new Regex("<[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public IEnumerable<TranscriptItem> parse(string plain_data)
        {
            foreach (var xml_element in XDocument.Parse(plain_data).Root.Elements("text"))
            {
                var text = xml_element.Value;
                if (string.IsNullOrEmpty(text)) continue;

                text = HTML_TAG_REGEX.Replace(System.Web.HttpUtility.HtmlDecode(text), "");
                _ = float.TryParse(xml_element.Attribute(XName.Get("start")).Value, out var start);
                _ = float.TryParse(xml_element.Attribute(XName.Get("dur"))?.Value ?? "0.0", out var duration);

                yield return new TranscriptItem()
                {
                    Text = text,
                    Start = start,
                    Duration = duration
                };
            }
        }
    }
}
