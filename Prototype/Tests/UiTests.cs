using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using CoastNotebook;

class UiTests
{
    static int passed;
    static object Field(MainForm form, string name)
    {
        return typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
    }
    static void Invoke(MainForm form, string name)
    {
        typeof(MainForm).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { null, EventArgs.Empty });
        Application.DoEvents();
    }
    static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        Console.WriteLine("PASS UI: " + name); passed++;
    }
    static int CountButtons(Control root, string text)
    {
        int count = root is Button && root.Text == text ? 1 : 0;
        foreach (Control child in root.Controls) count += CountButtons(child, text);
        return count;
    }
    static bool HasLabel(Control root, string text)
    {
        if (root is Label && root.Text == text) return true;
        foreach (Control child in root.Controls) if (HasLabel(child, text)) return true;
        return false;
    }
    static void SaveBook(MainForm form, Book book)
    {
        typeof(MainForm).GetMethod("Persist", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { book });
    }
    static void Rename(MainForm form, string id)
    {
        typeof(MainForm).GetMethod("RenameCore", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { id });
    }
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string folder = args[0]; Directory.CreateDirectory(folder);
        Book seed = new Book(); seed.Settings.Configured = true; seed.Settings.Name = "Test Notebook";
        seed.Settings.CsvFolder = Path.Combine(folder, "CSV"); seed.Settings.PhotoFolder = Path.Combine(folder, "Photos");
        seed.Settings.ShowSuccess = false;
        seed.Settings.Kinds.Add(new Kind { Id = "A", Name = "Document", Unit = "items" });
        seed.AcknowledgedDate = DateTime.Today; Data.Save(Path.Combine(folder, "Records.xml"), seed);
        using (ConfigDialog settings = new ConfigDialog(seed.Settings))
        {
            settings.Show(); Application.DoEvents();
            Check(HasLabel(settings, "구간 명 (예시: 4구간)"), "Settings section label");
            Check(CountButtons(settings, "찾아보기") == 2, "Both folder fields have browse buttons");
            TextBox path = (TextBox)typeof(ConfigDialog).GetField("csv", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(settings);
            path.Text = Path.Combine(folder, "TypedFolder");
            Check(path.Text.EndsWith("TypedFolder") && !path.ReadOnly, "Folder path remains directly editable");
            using (Bitmap image = new Bitmap(settings.Width, settings.Height))
            { settings.DrawToBitmap(image, new Rectangle(0, 0, settings.Width, settings.Height)); image.Save(Path.Combine(folder, "settings.png")); }
            settings.Close();
        }
        using (MainForm form = new MainForm(folder))
        {
            form.Show(); Application.DoEvents();
            TextBox title = (TextBox)Field(form, "title"); title.Text = "Example document";
            Invoke(form, "AddItem"); Invoke(form, "AddItem");
            ListBox cart = (ListBox)Field(form, "cart");
            Check(cart.Items.Count == 1 && ((Item)cart.Items[0]).Count == 2, "Duplicate items merge");
            Invoke(form, "SaveEntry");
            Book saved = Data.Load(Path.Combine(folder, "Records.xml"));
            Check(saved.Entries.Count == 1 && saved.Entries[0].Items[0].Count == 2, "Actual form save");
            string id = saved.Entries[0].Id;
            Invoke(form, "AddItem");
            Check(((Book)Field(form, "book")).Entries[0].Items[0].Count == 2, "Unsaved cart does not mutate saved book");
            Invoke(form, "SaveEntry");
            saved = Data.Load(Path.Combine(folder, "Records.xml"));
            Check(saved.Entries.Count == 1 && saved.Entries[0].Id == id && saved.Entries[0].Items[0].Count == 3, "Edit keeps ID and updates quantity");
            ((CheckBox)Field(form, "complete")).Checked = true; Invoke(form, "SaveEntry");
            Check(((ListBox)Field(form, "records")).Items.Count == 0, "Completed records hidden by filter");
            ((CheckBox)Field(form, "showDone")).Checked = true;
            Check(((ListBox)Field(form, "records")).Items.Count == 1, "Completed filter restores visibility");
            ((DateTimePicker)Field(form, "date")).Value = DateTime.Now.AddDays(-1); Invoke(form, "SaveEntry");
            Check(((ListBox)Field(form, "records")).Items.Count == 0, "Date filter follows record date");
            ((CheckBox)Field(form, "allDays")).Checked = true;
            Check(((ListBox)Field(form, "records")).Items.Count == 1, "All dates filter");
            ((TextBox)Field(form, "search")).Text = "not-found";
            Check(((ListBox)Field(form, "records")).Items.Count == 0, "Search filter");
            ((TextBox)Field(form, "search")).Text = "";
            FlowLayoutPanel editor = (FlowLayoutPanel)Field(form, "editor");
            Control last = editor.Controls[editor.Controls.Count - 1];
            Check(last.Bottom <= editor.ClientSize.Height, "Editor does not clip last control");
            Check(CountButtons(form, "사진할당") == 1, "Photo assignment button caption");
            string photoFolder = Path.Combine(folder, "PhotoTest"); Directory.CreateDirectory(photoFolder);
            string first = Path.Combine(photoFolder, "old (1).jpg"); File.WriteAllText(first, "first photo");
            Book next = Data.Clone((Book)Field(form, "book")); next.Entries[0].Photos.Add(first); SaveBook(form, next);
            Rename(form, id);
            Book renamed = Data.Load(Path.Combine(folder, "Records.xml"));
            string bare = renamed.Entries[0].Photos[0];
            Check(Path.GetFileName(bare) == Data.PhotoFileName(renamed.Entries[0], 1, 1, ".jpg")
                && File.Exists(bare) && !File.Exists(first), "Legacy singleton suffix removed with link updated");
            string second = Path.Combine(photoFolder, "second.jpg"); File.WriteAllText(second, "second photo");
            next = Data.Clone((Book)Field(form, "book")); next.Entries[0].Photos.Add(second); SaveBook(form, next);
            Rename(form, id); renamed = Data.Load(Path.Combine(folder, "Records.xml"));
            Check(Path.GetFileName(renamed.Entries[0].Photos[0]).EndsWith(" (1).jpg")
                && Path.GetFileName(renamed.Entries[0].Photos[1]).EndsWith(" (2).jpg"), "Appending a second photo numbers both files");
            Check(File.ReadAllText(renamed.Entries[0].Photos[0]) == "first photo"
                && File.ReadAllText(renamed.Entries[0].Photos[1]) == "second photo", "Photo renaming preserves contents");
            string firstNumbered = renamed.Entries[0].Photos[0];
            Rename(form, id);
            Check(Data.Load(Path.Combine(folder, "Records.xml")).Entries[0].Photos[0] == firstNumbered,
                "Repeated photo rename is idempotent");
            next = Data.Clone((Book)Field(form, "book")); next.Entries[0].Title = "Changed document";
            string occupied = Path.Combine(photoFolder, Data.PhotoFileName(next.Entries[0], 2, 1, ".jpg"));
            File.WriteAllText(occupied, "do not overwrite"); SaveBook(form, next); Rename(form, id);
            renamed = Data.Load(Path.Combine(folder, "Records.xml"));
            Check(renamed.Entries[0].Photos[0] == firstNumbered && File.Exists(firstNumbered)
                && File.ReadAllText(occupied) == "do not overwrite", "Rename collision preserves source and unrelated destination");
            File.Delete(occupied); Rename(form, id);
            Check(Data.Load(Path.Combine(folder, "Records.xml")).Entries[0].Photos[0] == occupied,
                "Rename succeeds after collision is removed");
            using (Bitmap image = new Bitmap(form.Width, form.Height))
            { form.DrawToBitmap(image, new Rectangle(0, 0, form.Width, form.Height)); image.Save(Path.Combine(folder, "screen.png")); }
            Check(File.Exists(Path.Combine(folder, "screen.png")), "Rendered form snapshot");
            Console.WriteLine("FORM SIZE: " + form.Size + "; editor=" + editor.Size);
            form.Close();
        }
        Console.WriteLine("UI TOTAL PASSED: " + passed);
    }
}
