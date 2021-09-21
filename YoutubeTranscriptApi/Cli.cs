using System;
using System.Collections.Generic;

namespace YoutubeTranscriptApi
{
    //https://github.com/jdepoix/youtube-transcript-api/blob/master/youtube_transcript_api/_cli.py

    class YouTubeTranscriptCli
    {
        public YouTubeTranscriptCli(params string[] args)
        {
            throw new NotImplementedException();
        }

        public string run()
        {
            var parsed_args = this._parse_args();
            throw new NotImplementedException();
        }

        object _fetch_transcript(string[] parsed_args, Dictionary<string, string> proxies, string cookies, string video_id)
        {
            using (var youTubeTranscriptApi = new YouTubeTranscriptApi())
            {
                var transcript_list = youTubeTranscriptApi.list_transcripts(video_id, proxies: proxies, cookies: cookies);
            }
            throw new NotImplementedException();
        }

        object _parse_args()
        {
            throw new NotImplementedException();
        }

        string _sanitize_video_ids(params string[] args)
        {
            throw new NotImplementedException();
        }
    }
}
