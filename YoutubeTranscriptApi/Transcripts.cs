using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace YoutubeTranscriptApi
{
    //https://github.com/jdepoix/youtube-transcript-api/blob/c5bf0132ffa2906cc1bf6d480a70ef799dedc209/youtube_transcript_api/_transcripts.py

    internal sealed class TranscriptListFetcher
    {
        private readonly HttpClient _httpClient;
        private readonly HttpClientHandler _httpClientHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="TranscriptListFetcher"/> class.
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="httpClientHandler"></param>
        public TranscriptListFetcher(HttpClient httpClient, HttpClientHandler httpClientHandler)
        {
            _httpClientHandler = httpClientHandler;
            _httpClient = httpClient;
        }

        public TranscriptList Fetch(string videoId)
        {
            return TranscriptList.build(
                _httpClient,
                videoId,
                extractCaptionsJson(fetchVideoHtml(videoId), videoId));
        }

        internal JsonElement extractCaptionsJson(string html, string videoId)
        {
            var splitted_html = html.Split("\"captions\":");

            if (splitted_html.Length <= 1)
            {
                if (html.Contains("class=\"g-recaptcha\""))
                {
                    throw new TooManyRequests(videoId);
                }

                if (!html.Contains("\"playabilityStatus\":"))
                {
                    throw new VideoUnavailable(videoId);
                }

                throw new TranscriptsDisabled(videoId);
            }

            var captions_json = JsonSerializer.Deserialize<JsonElement>(
                splitted_html[1].Split(",\"videoDetails")[0].Replace("\n", "")
                ).GetProperty("playerCaptionsTracklistRenderer");

            if (!captions_json.TryGetProperty("captionTracks", out _))
            {
                throw new NoTranscriptAvailable(videoId);
            }

            return captions_json;
        }

        internal void createConsentCookie(string html, string videoId)
        {
            var match = Regex.Match(html, "name=\"v\" value=\"(.*?)\"");
            if (!match.Success)
            {
                throw new FailedToCreateConsentCookie(videoId);
            }

            _httpClientHandler.CookieContainer.Add(new Cookie("CONSENT", $"YES+{match.Groups[1].Value}", null, ".youtube.com"));
        }

        public string fetchVideoHtml(string videoId)
        {
            var html = this.fetchHtml(videoId);
            if (html.Contains("action=\"https://consent.youtube.com/s\""))
            {
                createConsentCookie(html, videoId);
                html = this.fetchHtml(videoId);
                if (html.Contains("action=\"https://consent.youtube.com/s\""))
                {
                    throw new FailedToCreateConsentCookie(videoId);
                }
            }
            return html;
        }

        public string fetchHtml(string videoId)
        {
            string debug_url = string.Format(Settings.WATCH_URL, videoId);
            var raw_result = _httpClient.GetStringAsync(debug_url).Result;
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
        public string VideoId { get; }

        private readonly Dictionary<string, Transcript> _manuallyCreatedTranscripts;
        private readonly Dictionary<string, Transcript> _generatedTranscripts;
        private readonly List<Dictionary<string, string>> _translationLanguages;

        /// <summary>
        /// The constructor is only for internal use. Use the static build method instead.
        /// </summary>
        /// <param name="videoId"> the id of the video this TranscriptList is for</param>
        /// <param name="manuallyCreatedTranscripts">dict mapping language codes to the manually created transcripts</param>
        /// <param name="generatedTranscripts">dict mapping language codes to the generated transcripts</param>
        /// <param name="translationLanguages">list of languages which can be used for translatable languages</param>
        internal TranscriptList(string videoId, Dictionary<string, Transcript> manuallyCreatedTranscripts, Dictionary<string, Transcript> generatedTranscripts, List<Dictionary<string, string>> translationLanguages)
        {
            this.VideoId = videoId;
            this._manuallyCreatedTranscripts = manuallyCreatedTranscripts;
            this._generatedTranscripts = generatedTranscripts;
            this._translationLanguages = translationLanguages;
        }

        /// <summary>
        /// Factory method for TranscriptList.
        /// </summary>
        /// <param name="httpClient">http client which is used to make the transcript retrieving http calls</param>
        /// <param name="videoId">the id of the video this TranscriptList is for</param>
        /// <param name="captionsJson">the JSON parsed from the YouTube pages static HTML</param>
        /// <returns>the created <see cref="TranscriptList"/></returns>
        public static TranscriptList build(HttpClient httpClient, string videoId, JsonElement captionsJson)
        {
            var translationLanguages = new List<Dictionary<string, string>>();
            foreach (var translation_language in captionsJson.GetProperty("translationLanguages").EnumerateArray())
            {
                translationLanguages.Add(new Dictionary<string, string>
                {
                    { "language", translation_language.GetProperty("languageName").GetProperty("simpleText").GetString() },
                    { "language_code", translation_language.GetProperty("languageCode").GetString() }
                });
            }

            var manuallyCreatedTranscripts = new Dictionary<string, Transcript>();
            var generatedTranscripts = new Dictionary<string, Transcript>();

            foreach (var caption in captionsJson.GetProperty("captionTracks").EnumerateArray())
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
                    httpClient,
                    videoId,
                    caption.GetProperty("baseUrl").GetString(),
                    caption.GetProperty("name").GetProperty("simpleText").GetString(),
                    caption.GetProperty("languageCode").GetString(),
                    kind == "asr",
                    isTranslatable ? translationLanguages : new List<Dictionary<string, string>>());

                if (kind == "asr")
                {
                    generatedTranscripts.Add(caption.GetProperty("languageCode").GetString(), transcript);
                }
                else
                {
                    manuallyCreatedTranscripts.Add(caption.GetProperty("languageCode").GetString(), transcript);
                }
            }

            return new TranscriptList(
                videoId,
                manuallyCreatedTranscripts,
                generatedTranscripts,
                translationLanguages
            );
        }

        /// <inheritdoc/>
        public IEnumerator<Transcript> GetEnumerator()
        {
            foreach (var val in _manuallyCreatedTranscripts.Values.Concat(_generatedTranscripts.Values))
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
        /// <param name="languageCodes">A list of language codes in a descending priority. For example, if this is set to
        /// ['de', 'en'] it will first try to fetch the german transcript(de) and then fetch the english transcript(en) if
        /// it fails to do so.</param>
        /// <returns>the found Transcript</returns>
        public Transcript FindTranscript(IReadOnlyList<string> languageCodes)
        {
            return findTranscript(languageCodes, new[]
            {
                _manuallyCreatedTranscripts,
                _generatedTranscripts
            });
        }

        /// <summary>
        /// Finds a automatically generated transcript for a given language code.
        /// </summary>
        /// <param name="languageCodes">A list of language codes in a descending priority. For example, if this is set to
        /// ['de', 'en'] it will first try to fetch the german transcript(de) and then fetch the english transcript(en) if
        /// it fails to do so.</param>
        /// <returns>the found Transcript</returns>
        public Transcript FindGeneratedTranscript(IReadOnlyList<string> languageCodes)
        {
            return findTranscript(languageCodes, new[] { _generatedTranscripts });
        }

        /// <summary>
        /// Finds a manually created transcript for a given language code.
        /// </summary>
        /// <param name="languageCodes">A list of language codes in a descending priority. For example, if this is set to
        /// ['de', 'en'] it will first try to fetch the german transcript(de) and then fetch the english transcript(en) if
        /// it fails to do so.</param>
        /// <returns>the found Transcript</returns>
        public Transcript FindManuallyCreatedTranscript(IReadOnlyList<string> languageCodes)
        {
            return findTranscript(languageCodes, new[] { _manuallyCreatedTranscripts });
        }

        private Transcript findTranscript(IReadOnlyList<string> languageCodes, IReadOnlyList<Dictionary<string, Transcript>> transcriptDicts)
        {
            foreach (var languageCode in languageCodes)
            {
                foreach (var transcript_dict in transcriptDicts)
                {
                    if (transcript_dict.TryGetValue(languageCode, out var val))
                    {
                        return val;
                    }
                }
            }

            throw new NoTranscriptFound(this.VideoId, languageCodes, this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"For this video ({VideoId}) transcripts are available in the following languages:\n\n" +
                "(MANUALLY CREATED)\n" +
                $"{getLanguageDescription(_manuallyCreatedTranscripts.Values.Select(transcript => transcript.ToString()))}\n\n" +
                "(GENERATED)\n" +
                $"{getLanguageDescription(_generatedTranscripts.Values.Select(transcript => transcript.ToString()))}\n\n" +
                "(TRANSLATION LANGUAGES)\n" +
                $"{getLanguageDescription(_translationLanguages.Select(translationLanguage => $"{ translationLanguage["language_code"]} (\"{translationLanguage["language"]}\")"))}";
        }

        private string getLanguageDescription(IEnumerable<string> transcriptStrings)
        {
            if (!transcriptStrings.Any()) return "None";
            return string.Join("\n", transcriptStrings.Select(transcript => $" - {transcript}"));
        }
    }

    public class Transcript
    {
        private readonly IReadOnlyList<Dictionary<string, string>> translationLanguages;
        private readonly Dictionary<string, string> _translationLanguagesDict;
        private readonly HttpClient _httpClient;
        internal readonly string _url;

        public string VideoId { get; }
        public string Language { get; }
        public string LanguageCode { get; }
        public bool IsGenerated { get; }

        /// <summary>
        /// You probably don't want to initialize this directly. Usually you'll access Transcript objects using a
        /// TranscriptList.
        /// </summary>
        /// <param name="httpClient">http client which is used to make the transcript retrieving http calls</param>
        /// <param name="videoId">the id of the video this TranscriptList is for</param>
        /// <param name="url">the url which needs to be called to fetch the transcript</param>
        /// <param name="language">the name of the language this transcript uses</param>
        /// <param name="languageCode"></param>
        /// <param name="isGenerated"></param>
        /// <param name="translationLanguages"></param>
        public Transcript(HttpClient httpClient, string videoId, string url, string language, string languageCode,
            bool isGenerated, IReadOnlyList<Dictionary<string, string>> translationLanguages)
        {
            this._httpClient = httpClient;
            this.VideoId = videoId;
            this._url = url;
            this.Language = language;
            this.LanguageCode = languageCode;
            this.IsGenerated = isGenerated;
            this.translationLanguages = translationLanguages;
            this._translationLanguagesDict = new Dictionary<string, string>();
            foreach (var translation_language in translationLanguages)
            {
                _translationLanguagesDict.Add(translation_language["language_code"], translation_language["language"]);
            }
        }

        /// <summary>
        /// Loads the actual transcript data.
        /// </summary>
        /// <returns>a list of <see cref="TranscriptItem"/> containing the 'text', 'start' and 'duration' keys</returns>
        public IEnumerable<TranscriptItem> Fetch()
        {
            return new TranscriptParser().Parse(this._httpClient.GetStringAsync(this._url).Result);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{LanguageCode} (\"{Language}\"){(IsTranslatable ? "[TRANSLATABLE]" : "")}";
        }

        public bool IsTranslatable => translationLanguages.Count > 0;

        public Transcript Translate(string language_code)
        {
            if (!IsTranslatable)
            {
                throw new NotTranslatable(VideoId);
            }

            if (!_translationLanguagesDict.ContainsKey(language_code))
            {
                throw new TranslationLanguageNotAvailable(VideoId);
            }

            return new Transcript(
                _httpClient,
                VideoId,
                $"{_url}&tlang={language_code}",
                _translationLanguagesDict[language_code],
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

    internal class TranscriptParser
    {
        private readonly Regex HTML_TAG_REGEX = new Regex("<[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public IEnumerable<TranscriptItem> Parse(string plain_data)
        {
            foreach (var xmlElement in XDocument.Parse(plain_data).Root.Elements("text"))
            {
                var text = xmlElement.Value;
                if (string.IsNullOrEmpty(text)) continue;

                text = HTML_TAG_REGEX.Replace(System.Web.HttpUtility.HtmlDecode(text), "");
                _ = float.TryParse(xmlElement.Attribute(XName.Get("start")).Value, out var start);
                _ = float.TryParse(xmlElement.Attribute(XName.Get("dur"))?.Value ?? "0.0", out var duration);

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
