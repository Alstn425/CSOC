using System;
using System.IO;
using System.Text;
using CoastNotebook;

class CoreTests
{
    static int passed;
    static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name); passed++;
    }
    static void Fails(Action action, string name)
    {
        bool failed = false;
        try { action(); } catch { failed = true; }
        Check(failed, name);
    }
    static void Main(string[] args)
    {
        string folder = args[0]; Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "Records.xml");
        Book book = new Book(); book.Settings.CsvFolder = Path.Combine(folder, "CSV");
        Kind kind = new Kind { Id = "kindA", Name = "문서", Unit = "건" };
        book.Settings.Kinds.Add(kind);
        Entry entry = new Entry { Id = Guid.NewGuid().ToString("N"),
            Time = new DateTime(2026, 1, 1, 10, 30, 0), Title = "문서, \"확인\"",
            Note = "줄1\r\n줄2, \"따옴표\"" };
        entry.Items.Add(new Item { KindId = kind.Id, Name = kind.Name, Unit = kind.Unit, Count = 3 });
        book.Entries.Add(entry); Data.Queue(book, entry.Time); Data.Save(path, book);
        Book loaded = Data.Load(path);
        Check(loaded.Entries[0].Note == entry.Note && loaded.PendingDays.Count == 1, "XML round trip, pending export");
        Book clone = Data.Clone(book); clone.Entries[0].Items[0].Count = 8;
        Check(book.Entries[0].Items[0].Count == 3, "Independent edit copy");
        string original = File.ReadAllText(path);
        Fails(delegate { Data.AtomicWrite(path, delegate(Stream s) { s.WriteByte(1); throw new IOException("injected"); }); }, "Injected interrupted write");
        Check(File.ReadAllText(path) == original, "Original preserved after interrupted write");
        Check(Directory.GetFiles(folder, "*.tmp").Length == 0, "Temporary file cleanup");
        Data.ExportDay(book, entry.Time);
        Book invalid = Data.Clone(book); invalid.Entries[0].Items[0].Count = 1000;
        Fails(delegate { Data.Save(path, invalid); }, "Reject count above 999");
        Check(File.ReadAllText(path) == original, "Invalid edit preserves saved data");
        invalid = Data.Clone(book); invalid.Entries.Add(invalid.Entries[0]);
        Fails(delegate { Data.Save(path, invalid); }, "Reject duplicate IDs");
        Book edited = Data.Clone(book); edited.Entries[0].Note = "수정 완료";
        edited.Entries[0].Time = entry.Time.AddDays(1); Data.Queue(edited, edited.Entries[0].Time);
        Data.Save(path, edited); Data.ExportDay(edited, entry.Time); Data.ExportDay(edited, edited.Entries[0].Time);
        string oldCsv = Path.Combine(book.Settings.CsvFolder, "2026", "2026-01-01", "Records.csv");
        Check(File.ReadAllLines(oldCsv).Length == 1, "Date edit clears old day CSV");
        Check(Data.Load(path).Entries[0].Note == "수정 완료", "Edited record survives restart");
        string source = Path.Combine(folder, "source.jpg"); File.WriteAllText(source, "photo bytes");
        string copy = Path.Combine(folder, "destination.jpg"); Data.CopyVerified(source, copy);
        Check(File.Exists(source) && Data.Hash(source) == Data.Hash(copy), "Verified photo copy keeps source until commit");
        Fails(delegate { Data.CopyVerified(source, copy); }, "Photo collision rejected");
        Check(File.ReadAllText(copy) == "photo bytes", "Existing photo not overwritten or removed");
        edited.Entries[0].Photos.Add(copy); Data.Save(path, edited);
        edited.Entries.Clear(); Data.Save(path, edited); Data.ExportDay(edited, entry.Time.AddDays(1));
        Check(Data.Load(path).Entries.Count == 0 && File.Exists(copy), "Permanent record deletion retains photo");
        Check(Data.CsvCell(" =1+1").StartsWith("\"'"), "Spreadsheet formula protection");
        Check(Data.SafeName("a:b/c?d") == "a_b_c_d", "Windows filename cleanup");
        Book blocked = Data.Clone(book); blocked.Settings.CsvFolder = source; Data.Save(path, blocked);
        Fails(delegate { Data.ExportDay(blocked, entry.Time); }, "Export failure separated from primary save");
        Check(Data.Load(path).PendingDays.Count > 0, "Pending CSV work persists after export failure");
        File.WriteAllText(path, "<broken>");
        Fails(delegate { Data.Load(path); }, "Corrupt XML is rejected");
        Check(File.ReadAllText(path) == "<broken>", "Corrupt XML not silently replaced");
        File.WriteAllText(path, "<!DOCTYPE Book [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><Book>&x;</Book>");
        Fails(delegate { Data.Load(path); }, "DTD rejected");
        // Leave a rich CSV for an independent parser to verify.
        Data.ExportDay(book, entry.Time);
        Console.WriteLine("TOTAL PASSED: " + passed);
    }
}
