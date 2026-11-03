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
            using (Bitmap image = new Bitmap(form.Width, form.Height))
            { form.DrawToBitmap(image, new Rectangle(0, 0, form.Width, form.Height)); image.Save(Path.Combine(folder, "screen.png")); }
            Check(File.Exists(Path.Combine(folder, "screen.png")), "Rendered form snapshot");
            Console.WriteLine("FORM SIZE: " + form.Size + "; editor=" + editor.Size);
            form.Close();
        }
        Console.WriteLine("UI TOTAL PASSED: " + passed);
    }
}
