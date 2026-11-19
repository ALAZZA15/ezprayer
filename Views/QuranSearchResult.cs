using EzPrayer.Services;

namespace EzPrayer.Views
{
    public sealed class QuranSearchResult
    {
        public QuranSearchResult(SurahData surah, VerseData? verse)
        {
            Surah = surah;
            Verse = verse;
        }

        public SurahData Surah { get; }
        public VerseData? Verse { get; }
        public string Title => Verse == null
            ? $"{Surah.Name} ({Surah.NameTranslations?.Id})"
            : $"{Surah.Name} - Ayat {Verse.Number}";
        public string Description => Verse?.Translation ?? Surah.NameTranslations?.Id ?? string.Empty;
    }

    public sealed class QuranBookmarkItem
    {
        public QuranBookmarkItem(SurahData surah, VerseData verse)
        {
            Surah = surah;
            Verse = verse;
        }

        public SurahData Surah { get; }
        public VerseData Verse { get; }
        public int SurahNumber => Surah.Number;
        public int VerseNumber => Verse.Number;
        public string Title => $"{Surah.Name} · Ayat {Verse.Number}";
        public string Description => Verse.Translation;
    }
}
