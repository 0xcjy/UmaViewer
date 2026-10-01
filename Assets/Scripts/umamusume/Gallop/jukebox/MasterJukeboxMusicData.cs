using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mono.Data.Sqlite;

namespace Gallop
{
    /// <summary>
    /// UmaViewer adapter for the official MasterJukeboxMusicData table.
    /// The public API and row fields follow the official dummy class, while
    /// the database backend uses the Mono.Data.Sqlite connection already used
    /// by UmaDatabaseController.
    /// </summary>
    public sealed class MasterJukeboxMusicData
    {
        public const string TABLE_NAME = "jukebox_music_data";

        private readonly SqliteConnection _db;
        private bool _preloaded;
        private readonly HashSet<int> _notFounds = new HashSet<int>();
        private readonly Dictionary<int, JukeboxMusicData> _lazyPrimaryKeyDictionary =
            new Dictionary<int, JukeboxMusicData>();
        private readonly Dictionary<int, List<JukeboxMusicData>> _dictionaryWithVersionType =
            new Dictionary<int, List<JukeboxMusicData>>();

        public Dictionary<int, JukeboxMusicData> dictionary
        {
            get
            {
                ForcePreloadAllEntries();
                return _lazyPrimaryKeyDictionary;
            }
        }

        public MasterJukeboxMusicData(SqliteConnection db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public JukeboxMusicData Get(int musicId)
        {
            if (musicId == JukeboxUtil.NONE_MUSIC_ID)
                return null;

            if (_lazyPrimaryKeyDictionary.TryGetValue(musicId, out JukeboxMusicData cached))
                return cached;

            if (_notFounds.Contains(musicId))
                return null;

            JukeboxMusicData selected = SelectOne(musicId);
            if (selected == null)
            {
                _notFounds.Add(musicId);
                return null;
            }

            _lazyPrimaryKeyDictionary[musicId] = selected;
            return selected;
        }

        public JukeboxMusicData GetWithVersionType(int versionType)
        {
            List<JukeboxMusicData> list = GetListWithVersionType(versionType);
            return list != null && list.Count > 0 ? list[0] : null;
        }

        public List<JukeboxMusicData> GetListWithVersionType(int versionType)
        {
            if (_dictionaryWithVersionType.TryGetValue(versionType, out List<JukeboxMusicData> cached))
                return cached;

            List<JukeboxMusicData> list = ListSelectWithVersionType(versionType);
            _dictionaryWithVersionType[versionType] = list;
            return list;
        }

        public List<JukeboxMusicData> MaybeListWithVersionType(int versionType)
        {
            List<JukeboxMusicData> list = GetListWithVersionType(versionType);
            return list != null && list.Count > 0 ? list : null;
        }

        /// <summary>
        /// Viewer convenience method. The official dialog requests several
        /// VersionType lists and combines them. Until that exact dialog filter
        /// is fully reconstructed, this returns the union of every table row.
        /// </summary>
        public List<JukeboxMusicData> GetAll()
        {
            ForcePreloadAllEntries();
            return _lazyPrimaryKeyDictionary.Values
                .OrderBy(x => x.Sort)
                .ThenBy(x => x.MusicId)
                .ToList();
        }

        public void Unload()
        {
            _preloaded = false;
            _notFounds.Clear();
            _lazyPrimaryKeyDictionary.Clear();
            _dictionaryWithVersionType.Clear();
        }

        private JukeboxMusicData SelectOne(int musicId)
        {
            const string sql =
                "SELECT * FROM jukebox_music_data WHERE music_id = @music_id LIMIT 1";

            using (SqliteCommand command = _db.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.AddWithValue("@music_id", musicId);
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    return reader.Read() ? CreateOrmByQueryResult(reader) : null;
                }
            }
        }

        private List<JukeboxMusicData> ListSelectWithVersionType(int versionType)
        {
            const string sql =
                "SELECT * FROM jukebox_music_data " +
                "WHERE version_type = @version_type ORDER BY sort, music_id";

            var list = new List<JukeboxMusicData>();
            using (SqliteCommand command = _db.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.AddWithValue("@version_type", versionType);
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        JukeboxMusicData item = CreateOrmByQueryResult(reader);
                        if (_lazyPrimaryKeyDictionary.TryGetValue(item.MusicId, out JukeboxMusicData cached))
                            item = cached;
                        else
                            _lazyPrimaryKeyDictionary.Add(item.MusicId, item);

                        list.Add(item);
                    }
                }
            }

            return list;
        }

        private void ForcePreloadAllEntries()
        {
            if (_preloaded)
                return;

            _preloaded = true;
            const string sql = "SELECT * FROM jukebox_music_data ORDER BY sort, music_id";

            using (SqliteCommand command = _db.CreateCommand())
            {
                command.CommandText = sql;
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        JukeboxMusicData item = CreateOrmByQueryResult(reader);
                        if (!_lazyPrimaryKeyDictionary.ContainsKey(item.MusicId))
                            _lazyPrimaryKeyDictionary.Add(item.MusicId, item);
                    }
                }
            }
        }

        private static JukeboxMusicData CreateOrmByQueryResult(SqliteDataReader reader)
        {
            return new JukeboxMusicData(
                musicId: ReadInt(reader, "music_id"),
                sort: ReadInt(reader, "sort"),
                conditionType: ReadInt(reader, "condition_type"),
                isHidden: ReadInt(reader, "is_hidden"),
                versionType: ReadInt(reader, "version_type"),
                requestType: ReadInt(reader, "request_type"),
                lampColor: ReadInt(reader, "lamp_color"),
                lampAnimation: ReadInt(reader, "lamp_animation"),
                nameTextureLength: ReadInt(reader, "name_texture_length"),
                songType: (byte)ReadInt(reader, "song_type"),
                bgmCueNameShort: ReadString(reader, "bgm_cue_name_short"),
                bgmCuesheetNameShort: ReadString(reader, "bgm_cuesheet_name_short"),
                bgmCueNameGamesize: ReadString(reader, "bgm_cue_name_gamesize"),
                bgmCuesheetNameGamesize: ReadString(reader, "bgm_cuesheet_name_gamesize"),
                shortLength: ReadInt(reader, "short_length"),
                alterJacket: ReadInt(reader, "alter_jacket"),
                startDate: ReadLong(reader, "start_date"),
                endDate: ReadLong(reader, "end_date"));
        }

        private static int ReadInt(SqliteDataReader reader, string name, int fallback = 0)
        {
            int ordinal = FindOrdinal(reader, name);
            if (ordinal < 0 || reader.IsDBNull(ordinal))
                return fallback;

            try
            {
                return Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static long ReadLong(SqliteDataReader reader, string name, long fallback = 0L)
        {
            int ordinal = FindOrdinal(reader, name);
            if (ordinal < 0 || reader.IsDBNull(ordinal))
                return fallback;

            try
            {
                return Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static string ReadString(SqliteDataReader reader, string name)
        {
            int ordinal = FindOrdinal(reader, name);
            if (ordinal < 0 || reader.IsDBNull(ordinal))
                return string.Empty;

            return Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static int FindOrdinal(SqliteDataReader reader, string name)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        public sealed class JukeboxMusicData
        {
            public readonly int MusicId;
            public readonly int Sort;
            public readonly int ConditionType;
            public readonly int IsHidden;
            public readonly int VersionType;
            public readonly int RequestType;
            public readonly int LampColor;
            public readonly int LampAnimation;
            public readonly int NameTextureLength;
            public readonly byte SongType;
            public readonly string BgmCueNameShort;
            public readonly string BgmCuesheetNameShort;
            public readonly string BgmCueNameGamesize;
            public readonly string BgmCuesheetNameGamesize;
            public readonly int ShortLength;
            public readonly int AlterJacket;
            public readonly long StartDate;
            public readonly long EndDate;

            public JukeboxMusicData(
                int musicId = 0,
                int sort = 0,
                int conditionType = 0,
                int isHidden = 0,
                int versionType = 0,
                int requestType = 0,
                int lampColor = 0,
                int lampAnimation = 0,
                int nameTextureLength = 0,
                byte songType = 0,
                string bgmCueNameShort = "",
                string bgmCuesheetNameShort = "",
                string bgmCueNameGamesize = "",
                string bgmCuesheetNameGamesize = "",
                int shortLength = 0,
                int alterJacket = 0,
                long startDate = 0L,
                long endDate = 0L)
            {
                MusicId = musicId;
                Sort = sort;
                ConditionType = conditionType;
                IsHidden = isHidden;
                VersionType = versionType;
                RequestType = requestType;
                LampColor = lampColor;
                LampAnimation = lampAnimation;
                NameTextureLength = nameTextureLength;
                SongType = songType;
                BgmCueNameShort = bgmCueNameShort ?? string.Empty;
                BgmCuesheetNameShort = bgmCuesheetNameShort ?? string.Empty;
                BgmCueNameGamesize = bgmCueNameGamesize ?? string.Empty;
                BgmCuesheetNameGamesize = bgmCuesheetNameGamesize ?? string.Empty;
                ShortLength = shortLength;
                AlterJacket = alterJacket;
                StartDate = startDate;
                EndDate = endDate;
            }

            public bool TryGetPreferredAudio(out string cuesheetName, out string cueName)
            {
                // The home jukebox table explicitly provides a short version.
                // Use it first and only fall back to game-size when the short
                // pair is missing from the current regional master.
                return TryGetAudio(false, out cuesheetName, out cueName);
            }

            public bool HasShortAudio =>
                !string.IsNullOrWhiteSpace(BgmCuesheetNameShort) &&
                !string.IsNullOrWhiteSpace(BgmCueNameShort);

            public bool HasGameSizeAudio =>
                !string.IsNullOrWhiteSpace(BgmCuesheetNameGamesize) &&
                !string.IsNullOrWhiteSpace(BgmCueNameGamesize);

            public bool TryGetAudio(bool useGameSize, out string cuesheetName, out string cueName)
            {
                if (useGameSize && HasGameSizeAudio)
                {
                    cuesheetName = BgmCuesheetNameGamesize;
                    cueName = BgmCueNameGamesize;
                    return true;
                }

                if (!useGameSize && HasShortAudio)
                {
                    cuesheetName = BgmCuesheetNameShort;
                    cueName = BgmCueNameShort;
                    return true;
                }

                // Some regional rows only contain one version. Keep those songs
                // playable even when the requested version is unavailable.
                if (HasGameSizeAudio)
                {
                    cuesheetName = BgmCuesheetNameGamesize;
                    cueName = BgmCueNameGamesize;
                    return true;
                }

                if (HasShortAudio)
                {
                    cuesheetName = BgmCuesheetNameShort;
                    cueName = BgmCueNameShort;
                    return true;
                }

                cuesheetName = string.Empty;
                cueName = string.Empty;
                return false;
            }
        }
    }
}
