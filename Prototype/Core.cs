using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace CoastNotebook
{
    public class Kind
    {
        public string Id = "";
        public string Name = "";
        public string Unit = "";
        public override string ToString() { return Name + " (" + Unit + ")"; }
    }

    public class Item
    {
        public string KindId = "";
        public string Name = "";
        public string Unit = "";
        public int Count;
        public override string ToString() { return Name + " " + Count + Unit; }
    }

    public class Entry
    {
        public string Id = "";
        public DateTime Time;
        public string Title = "";
        public string Note = "";
        public bool Complete;
        public List<Item> Items = new List<Item>();
        public List<string> Photos = new List<string>();
        public override string ToString()
        {
            return Time.ToString("MM-dd HH:mm") + " " + Title + " "
                + Describe() + (Complete ? " [완료]" : " [진행]");
        }
        public string Describe()
        {
            List<string> parts = new List<string>();
            foreach (Item item in Items) parts.Add(item.ToString());
            return String.Join(", ", parts.ToArray());
        }
    }

    public class Settings
    {
        public bool Configured;
        public string Name = "기록장";
        public string CsvFolder = "";
        public string PhotoFolder = "";
        public bool CopyText;
        public bool ShowSuccess = true;
        public List<Kind> Kinds = new List<Kind>();
        public string Alarms = "";
    }

    public class Book
    {
        public int Version = 1;
        public Settings Settings = new Settings();
        public List<Entry> Entries = new List<Entry>();
        public List<string> PendingDays = new List<string>();
        public string Help = "";
        public DateTime AcknowledgedDate;
        public List<string> AlarmAcknowledgements = new List<string>();
    }

    public static class Data
    {
        static readonly XmlSerializer Serializer = new XmlSerializer(typeof(Book));

        public static Book Clone(Book book)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                WriteXml(memory, book);
                memory.Position = 0;
                return (Book)Serializer.Deserialize(memory);
            }
        }

        public static Book Load(string path)
        {
            if (!File.Exists(path)) return new Book();
            XmlReaderSettings options = new XmlReaderSettings();
            options.DtdProcessing = DtdProcessing.Prohibit;
            options.XmlResolver = null;
            using (XmlReader reader = XmlReader.Create(path, options))
            {
                Book book = (Book)Serializer.Deserialize(reader);
                Validate(book);
                return book;
            }
        }

        public static void Validate(Book book)
        {
            if (book == null || book.Version != 1 || book.Settings == null
                || book.Entries == null || book.PendingDays == null
                || book.AlarmAcknowledgements == null)
                throw new InvalidDataException("지원하지 않거나 손상된 원본입니다.");
            HashSet<string> ids = new HashSet<string>();
            foreach (Entry entry in book.Entries)
            {
                Guid parsedId;
                if (entry == null || !Guid.TryParseExact(entry.Id, "N", out parsedId)
                    || !ids.Add(entry.Id) || entry.Items == null || entry.Photos == null
                    || entry.Title == null || entry.Note == null || entry.Time.Year < 1753 || entry.Time.Year > 9998)
                    throw new InvalidDataException("기록 ID 또는 필드가 올바르지 않습니다.");
                HashSet<string> kinds = new HashSet<string>();
                foreach (Item item in entry.Items)
                    if (item == null || String.IsNullOrWhiteSpace(item.KindId)
                        || !kinds.Add(item.KindId) || item.Name == null || item.Unit == null
                        || item.Count < 0 || item.Count > 999)
                        throw new InvalidDataException("기록의 항목 또는 수량이 올바르지 않습니다.");
            }
            foreach (string day in book.PendingDays) ParseDay(day);
        }

        public static void AtomicWrite(string path, Action<Stream> write)
        {
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temp,
                    FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    write(stream);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }

        public static void Save(string path, Book book)
        {
            Validate(book);
            AtomicWrite(path, delegate(Stream stream) { WriteXml(stream, book); });
        }

        static void WriteXml(Stream stream, Book book)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = new UTF8Encoding(false);
            settings.Indent = true;
            settings.NewLineHandling = NewLineHandling.Entitize;
            settings.CloseOutput = false;
            using (XmlWriter writer = XmlWriter.Create(stream, settings)) Serializer.Serialize(writer, book);
        }

        public static string Day(DateTime value) { return value.ToString("yyyy-MM-dd"); }
        public static DateTime ParseDay(string value)
        {
            return DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        public static void Queue(Book book, DateTime day)
        {
            string text = Day(day);
            if (!book.PendingDays.Contains(text)) book.PendingDays.Add(text);
        }

        public static string CsvCell(string value)
        {
            string text = value ?? "";
            string trimmed = text.TrimStart();
            if ((trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0)
                || (text.Length > 0 && "\t\r\n".IndexOf(text[0]) >= 0))
                text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        static void CsvLine(StringBuilder text, List<string> fields)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(CsvCell(fields[i]));
            }
            text.Append("\r\n");
        }

        public static string ExportDay(Book book, DateTime day)
        {
            string folder = Path.Combine(book.Settings.CsvFolder, day.ToString("yyyy"), Day(day));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "Records.csv");
            // IDs identify columns even after a type is renamed or removed.
            SortedDictionary<string, string> kinds = new SortedDictionary<string, string>();
            foreach (Kind kind in book.Settings.Kinds) kinds[kind.Id] = kind.Name;
            foreach (Entry entry in book.Entries)
                foreach (Item item in entry.Items)
                    if (!kinds.ContainsKey(item.KindId)) kinds[item.KindId] = item.Name;

            List<string> header = new List<string>(new string[] {
                "ID", "일시", "제목", "상태", "내역", "비고", "사진수" });
            foreach (KeyValuePair<string, string> kind in kinds)
                header.Add(kind.Value + " [" + kind.Key + "] 수량");
            StringBuilder text = new StringBuilder();
            CsvLine(text, header);
            foreach (Entry entry in book.Entries)
            {
                if (entry.Time.Date != day.Date) continue;
                List<string> row = new List<string>(new string[] {
                    entry.Id, entry.Time.ToString("yyyy-MM-dd HH:mm"), entry.Title,
                    entry.Complete ? "완료" : "진행", entry.Describe(), entry.Note,
                    entry.Photos.Count.ToString(CultureInfo.InvariantCulture) });
                foreach (string kind in kinds.Keys)
                {
                    int count = 0;
                    foreach (Item item in entry.Items)
                        if (item.KindId == kind) count += item.Count;
                    row.Add(count.ToString(CultureInfo.InvariantCulture));
                }
                CsvLine(text, row);
            }
            byte[] bytes = new UTF8Encoding(true).GetBytes(text.ToString());
            AtomicWrite(path, delegate(Stream stream)
            {
                byte[] bom = new UTF8Encoding(true).GetPreamble();
                stream.Write(bom, 0, bom.Length);
                stream.Write(bytes, 0, bytes.Length);
            });
            return path;
        }

        public static string SafeName(string value)
        {
            StringBuilder result = new StringBuilder();
            foreach (char c in value)
                result.Append(c < 32 || "<>:\"/\\|?*".IndexOf(c) >= 0 ? '_' : c);
            string name = result.ToString().Trim().TrimEnd('.', ' ');
            if (name.Length > 80) name = name.Substring(0, 80).TrimEnd('.', ' ');
            return name.Length == 0 ? "기록" : name;
        }

        public static string PhotoStem(Entry entry)
        {
            return SafeName(entry.Time.ToString("yyyyMMdd_HHmm") + " "
                + entry.Title + " " + entry.Describe()) + "_" + entry.Id.Substring(0, 8);
        }

        public static string UniquePath(string folder, string stem, string extension)
        {
            for (int i = 1; i < 100000; i++)
            {
                string path = Path.Combine(folder, stem + " (" + i + ")" + extension);
                if (!File.Exists(path) && !Directory.Exists(path)) return path;
            }
            throw new IOException("사용 가능한 파일명을 만들 수 없습니다.");
        }

        public static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return Convert.ToBase64String(sha.ComputeHash(stream));
        }

        public static void CopyVerified(string source, string destination)
        {
            bool created = false;
            try
            {
                using (FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true;
                    input.CopyTo(output);
                    output.Flush(true);
                }
                if (Hash(source) != Hash(destination)) throw new IOException("사진 복사 검증에 실패했습니다.");
            }
            catch
            {
                if (created) { try { File.Delete(destination); } catch { } }
                throw;
            }
        }
    }
}
