using System;
using System.Data;
using System.Globalization;

namespace Gallop
{
    public static class JukeboxUtil
    {
        public const int NONE_MUSIC_ID = 0;

        public enum LampType
        {
            None = 0,
            Fire = 1,
            Serious = 2,
            Well = 3,
            Cool = 4,
            Cute = 5,
            Max = 6
        }

        public enum LampAnimationType
        {
            None = 0,
            Bounce = 1,
            Slowly = 2,
            Max = 3
        }

        public enum SongType
        {
            None = 0,
            Pops = 1,
            PopRock = 2,
            HardRock = 3,
            Electro = 4,
            Geek = 5,
            Chill = 6,
            Max = 7
        }

        public enum EffectType
        {
            None = 0,
            Bounce = 1,
            Slowly = 2,
            Max = 3
        }

        /// <summary>
        /// The official client can replace some song titles with an image.
        /// UmaViewer currently has no title-image implementation, so live song
        /// names are read from text_data and all other songs fall back to cue name.
        /// </summary>
        public static string ResolveSongTitle(
            MasterJukeboxMusicData.JukeboxMusicData music,
            System.Collections.Generic.IEnumerable<DataRow> liveData)
        {
            if (music == null)
                return string.Empty;

            if (liveData != null)
            {
                foreach (DataRow row in liveData)
                {
                    if (row == null || !row.Table.Columns.Contains("music_id"))
                        continue;

                    int musicId;
                    try
                    {
                        musicId = Convert.ToInt32(row["music_id"], CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        continue;
                    }

                    if (musicId != music.MusicId)
                        continue;

                    if (row.Table.Columns.Contains("songname"))
                    {
                        string title = Convert.ToString(row["songname"], CultureInfo.InvariantCulture);
                        if (!string.IsNullOrWhiteSpace(title))
                            return title;
                    }
                }
            }

            string cueName = !string.IsNullOrWhiteSpace(music.BgmCueNameShort)
                ? music.BgmCueNameShort
                : music.BgmCueNameGamesize;

            return string.IsNullOrWhiteSpace(cueName)
                ? $"Music {music.MusicId}"
                : cueName;
        }

        public static bool IsInOpenPeriod(
            MasterJukeboxMusicData.JukeboxMusicData music,
            DateTimeOffset now)
        {
            if (music == null)
                return false;

            if (TryParseMasterDate(music.StartDate, out DateTimeOffset start) && now < start)
                return false;

            if (TryParseMasterDate(music.EndDate, out DateTimeOffset end) && now > end)
                return false;

            return true;
        }

        private static bool TryParseMasterDate(long raw, out DateTimeOffset value)
        {
            value = default;
            if (raw <= 0)
                return false;

            string text = raw.ToString(CultureInfo.InvariantCulture);
            if (text.Length == 14 &&
                DateTime.TryParseExact(
                    text,
                    "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime ymd))
            {
                // Japanese master data normally stores these values in JST.
                value = new DateTimeOffset(
                    DateTime.SpecifyKind(ymd, DateTimeKind.Unspecified),
                    TimeSpan.FromHours(9));
                return true;
            }

            try
            {
                if (raw >= 100000000000L)
                    value = DateTimeOffset.FromUnixTimeMilliseconds(raw);
                else if (raw >= 100000000L)
                    value = DateTimeOffset.FromUnixTimeSeconds(raw);
                else
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
