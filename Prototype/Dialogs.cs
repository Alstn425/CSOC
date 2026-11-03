using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace CoastNotebook
{
    public class TextDialog : Form
    {
        public TextBox Input = new TextBox();
        public TextDialog(string title, string text)
        {
            Text = title; Width = 560; Height = 470;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("맑은 고딕", 9F);
            Input.Multiline = true; Input.ScrollBars = ScrollBars.Both;
            Input.Dock = DockStyle.Fill; Input.Text = text;
            Button save = new Button();
            save.Text = "적용"; save.Dock = DockStyle.Bottom; save.Height = 36;
            save.DialogResult = DialogResult.OK;
            Controls.Add(Input); Controls.Add(save);
        }
    }

    public class ConfigDialog : Form
    {
        public Settings Value;
        TextBox name, csv, photos, kinds, alarms;
        CheckBox copy, success;
        Settings previous;

        public ConfigDialog(Settings current)
        {
            previous = current;
            Text = "기록장 설정"; Width = 540; Height = 670;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("맑은 고딕", 9F);
            FlowLayoutPanel panel = new FlowLayoutPanel();
            panel.Dock = DockStyle.Fill; panel.AutoScroll = true;
            panel.FlowDirection = FlowDirection.TopDown; panel.WrapContents = false;
            Controls.Add(panel);
            name = Field(panel, "기록장 이름", current.Name, 25);
            csv = Field(panel, "CSV 출력 폴더 (절대 경로)", current.CsvFolder, 25);
            photos = Field(panel, "사진 이동 폴더 (절대 경로)", current.PhotoFolder, 25);
            List<string> lines = new List<string>();
            foreach (Kind kind in current.Kinds) lines.Add(kind.Name + "|" + kind.Unit);
            kinds = Field(panel, "항목: 한 줄에 이름|단위 (최대 50개)", String.Join("\r\n", lines.ToArray()), 130);
            alarms = Field(panel, "일반 일정: 한 줄에 HH:mm|일정 이름 (빈칸이면 알림 없음)", current.Alarms, 100);
            copy = new CheckBox(); copy.Text = "저장 후 문장 복사";
            copy.Checked = current.CopyText; copy.Width = 440; panel.Controls.Add(copy);
            success = new CheckBox(); success.Text = "저장 결과 팝업 표시";
            success.Checked = current.ShowSuccess; success.Width = 440; panel.Controls.Add(success);
            Label notice = new Label(); notice.Width = 470; notice.Height = 45;
            notice.Text = "유형 이름을 바꾸면 새 유형으로 등록됩니다. 과거 기록은 유지됩니다.\r\n외부로 복사한 파일은 설정 변경이나 기록 삭제로 자동 제거되지 않습니다.";
            panel.Controls.Add(notice);
            Button save = new Button(); save.Text = "설정 저장"; save.Size = new Size(470, 38);
            save.Click += Save; panel.Controls.Add(save);
        }

        TextBox Field(Control panel, string label, string text, int height)
        {
            Label title = new Label(); title.Text = label; title.Width = 470; title.Height = 22;
            panel.Controls.Add(title);
            TextBox box = new TextBox(); box.Text = text; box.Width = 470;
            box.Multiline = height > 30; box.Height = height;
            if (box.Multiline) box.ScrollBars = ScrollBars.Vertical;
            panel.Controls.Add(box); return box;
        }

        public static List<string[]> ParseAlarms(string text)
        {
            List<string[]> result = new List<string[]>();
            HashSet<string> unique = new HashSet<string>();
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim(); if (line.Length == 0) continue;
                string[] parts = line.Split(new char[] { '|' }, 2);
                DateTime parsed;
                if (parts.Length != 2 || parts[1].Trim().Length == 0
                    || !DateTime.TryParseExact(parts[0].Trim(), "HH:mm", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out parsed) || !unique.Add(line))
                    throw new FormatException("일정 형식은 HH:mm|일정 이름 입니다. 같은 줄을 중복하지 마세요.");
                result.Add(new string[] { parts[0].Trim(), parts[1].Trim() });
            }
            return result;
        }

        public static void Validate(Settings settings)
        {
            if (settings == null || !settings.Configured || String.IsNullOrWhiteSpace(settings.Name)
                || String.IsNullOrWhiteSpace(settings.CsvFolder) || !Path.IsPathRooted(settings.CsvFolder)
                || String.IsNullOrWhiteSpace(settings.PhotoFolder) || !Path.IsPathRooted(settings.PhotoFolder)
                || settings.Kinds == null || settings.Kinds.Count == 0 || settings.Kinds.Count > 50)
                throw new FormatException("이름, 출력 폴더와 1~50개의 항목을 설정하세요.");
            HashSet<string> ids = new HashSet<string>();
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Kind kind in settings.Kinds)
                if (kind == null || String.IsNullOrWhiteSpace(kind.Name) || String.IsNullOrWhiteSpace(kind.Id)
                    || !ids.Add(kind.Id) || !names.Add(kind.Name) || String.IsNullOrWhiteSpace(kind.Unit))
                    throw new FormatException("유형 이름, 단위와 ID를 확인하세요. 유형 이름은 중복할 수 없습니다.");
            ParseAlarms(settings.Alarms);
        }

        void Save(object sender, EventArgs e)
        {
            try
            {
                Settings settings = new Settings(); settings.Configured = true;
                settings.Name = name.Text.Trim(); settings.CsvFolder = csv.Text.Trim();
                settings.PhotoFolder = photos.Text.Trim(); settings.CopyText = copy.Checked;
                settings.ShowSuccess = success.Checked; settings.Alarms = alarms.Text;
                foreach (string raw in kinds.Text.Split('\n'))
                {
                    string line = raw.Trim(); if (line.Length == 0) continue;
                    string[] parts = line.Split('|');
                    if (parts.Length != 2 || parts[0].Trim().Length == 0 || parts[1].Trim().Length == 0)
                        throw new FormatException("항목은 한 줄에 이름|단위로 입력하세요.");
                    Kind kind = new Kind(); kind.Name = parts[0].Trim(); kind.Unit = parts[1].Trim();
                    kind.Id = Guid.NewGuid().ToString("N");
                    foreach (Kind old in previous.Kinds)
                        if (old.Name == kind.Name && old.Unit == kind.Unit) kind.Id = old.Id;
                    settings.Kinds.Add(kind);
                }
                Validate(settings);
                Value = settings; DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "설정 확인"); }
        }
    }
}
