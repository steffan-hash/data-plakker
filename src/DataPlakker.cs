// DataPlakker.cs - screenshot -> plakbare tekst/tabel op het klembord
//
// Wat:    draait onzichtbaar op de achtergrond (icoon in het systeemvak) en
//         wacht op Ctrl+Alt+P. Dan: scherm bevriezen, gebied selecteren,
//         lokaal uitlezen met Windows-OCR, en het resultaat op het klembord
//         zetten (HTML-tabel voor mail, uitgelijnde tekst voor chat).
//         Esc of rechtermuisknop = annuleren.
// Input:  niets; start bij inloggen (zie scripts\install.ps1).
//         "DataPlakker.exe --selftest" draait in plaats daarvan de controles.
// Output: klembord gevuld + een kort geluidje. Niets wordt opgeslagen of
//         verstuurd; de schermafbeelding bestaat alleen in het geheugen.
//
// Geschreven in C# 5, omdat dat de compiler is die in Windows zelf zit
// (geen ontwikkelpakket nodig). Daarom geen nieuwere schrijfwijzen zoals $"".

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using WF = Windows.Foundation;
using Rect = Windows.Foundation.Rect;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Security.Cryptography;

static class Program
{
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [STAThread]
    static int Main(string[] args)
    {
        // Zonder dit ziet het programma op een geschaald scherm (bv. 125%) een
        // kleinere, verschoven versie van het scherm en klopt de selectie niet.
        SetProcessDpiAwarenessContext(new IntPtr(-4));   // -4 = per beeldscherm (v2)

        if (args.Contains("--selftest")) return SelfTest.Run();

        // Maar één exemplaar tegelijk; een tweede start doet niets.
        bool first;
        using (var mutex = new Mutex(true, "DataPlakker.SingleInstance", out first))
        {
            if (!first) return 0;
            Application.EnableVisualStyles();
            Application.Run(new TrayApp());
        }
        return 0;
    }
}

// Het achtergrondgedeelte: systeemvak-icoon + luisteren naar de sneltoets.
class TrayApp : ApplicationContext
{
    readonly NotifyIcon tray;
    readonly HotkeyListener listener;
    bool busy;

    public TrayApp()
    {
        listener = new HotkeyListener(OnHotkey);
        if (!listener.Registered)
        {
            MessageBox.Show("Ctrl+Alt+P is al in gebruik door een ander programma.\n" +
                "Staat de oude data-plakker-snelkoppeling nog in het Startmenu? Draai dan scripts\\install.ps1 opnieuw.",
                "data-plakker");
            ExitThreadCore();
            return;
        }

        // Icoon zodat zichtbaar is dát het draait, en om het af te sluiten.
        var menu = new ContextMenuStrip();
        menu.Items.Add("Afsluiten", null, delegate { ExitThread(); });
        tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "data-plakker (Ctrl+Alt+P)",
            ContextMenuStrip = menu,
            Visible = true
        };
    }

    void OnHotkey()
    {
        // Nogmaals drukken terwijl het selectiekader al openstaat negeren.
        if (busy) return;
        busy = true;
        try { Capture.Run(); }
        catch (Exception e) { MessageBox.Show("Er ging iets mis:\n" + e.Message, "data-plakker"); }
        finally { busy = false; }
    }

    protected override void ExitThreadCore()
    {
        if (tray != null) tray.Dispose();
        listener.Dispose();
        base.ExitThreadCore();
    }
}

// Onzichtbaar venster dat Windows de sneltoets laat melden (RegisterHotKey).
// Windows zelf luistert; dit programma doet niets tot de toets valt.
class HotkeyListener : NativeWindow, IDisposable
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_NOREPEAT = 0x4000;
    const int WM_HOTKEY = 0x312;

    readonly Action onHotkey;
    public readonly bool Registered;

    public HotkeyListener(Action onHotkey)
    {
        this.onHotkey = onHotkey;
        CreateHandle(new CreateParams());
        Registered = RegisterHotKey(Handle, 1, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.P);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY) onHotkey();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterHotKey(Handle, 1);
        DestroyHandle();
    }
}

// Eén keer selecteren -> uitlezen -> klembord.
static class Capture
{
    public static void Run()
    {
        // Foto van alle schermen samen; die tonen we als "bevroren" achtergrond,
        // zodat menu's/tooltips niet verdwijnen tijdens het selecteren.
        Rectangle vs = SystemInformation.VirtualScreen;
        Rectangle sel;
        Bitmap crop;
        using (var shot = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(shot)) g.CopyFromScreen(vs.Left, vs.Top, 0, 0, shot.Size);
            using (var form = new SelectionForm(shot, vs))
            {
                form.ShowDialog();
                sel = form.Selection;
            }
            if (sel.IsEmpty) return;
            crop = shot.Clone(sel, PixelFormat.Format32bppArgb);
        }

        Grid grid;
        using (crop) grid = Layout.ToGrid(Ocr.Read(crop));

        if (grid.Rows.Count == 0)
        {
            System.Media.SystemSounds.Hand.Play();
            MessageBox.Show("Geen tekst gevonden in de selectie.", "data-plakker");
            return;
        }
        Layout.SetClipboard(Layout.PlainText(grid), Layout.Html(grid));
        System.Media.SystemSounds.Asterisk.Play();
    }
}

// Schermvullend venster met de bevroren foto; muis indrukken = beginpunt,
// slepen = kader, loslaten = klaar. Buiten de selectie iets donkerder.
class SelectionForm : Form
{
    readonly SolidBrush dim = new SolidBrush(Color.FromArgb(90, 0, 0, 0));
    readonly Pen pen = new Pen(Color.FromArgb(255, 245, 190, 0), 2);
    Point? start;
    Rectangle rect;
    public Rectangle Selection { get; private set; }

    public SelectionForm(Bitmap shot, Rectangle bounds)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;
        BackgroundImage = shot;
        DoubleBuffered = true;
        KeyPreview = true;
    }

    protected override void OnShown(EventArgs e) { base.OnShown(e); Activate(); }

    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) { Close(); return; }
        start = e.Location;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (start == null) return;
        Point s = start.Value;
        rect = Rectangle.FromLTRB(Math.Min(s.X, e.X), Math.Min(s.Y, e.Y), Math.Max(s.X, e.X), Math.Max(s.Y, e.Y));
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (start != null && rect.Width > 4 && rect.Height > 4) Selection = rect;
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (var region = new Region(ClientRectangle))
        {
            if (!rect.IsEmpty) region.Exclude(rect);
            e.Graphics.FillRegion(dim, region);
        }
        if (!rect.IsEmpty) e.Graphics.DrawRectangle(pen, rect);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { dim.Dispose(); pen.Dispose(); }
        base.Dispose(disposing);
    }
}

class Word
{
    public string Text;
    public double X, Y, W, H;
    public Word(string text, double x, double y, double w, double h) { Text = text; X = x; Y = y; W = w; H = h; }
}

class Grid
{
    public bool IsTable;
    public List<string[]> Rows = new List<string[]>();
}

// Windows-OCR: afbeelding -> losse woorden met positie. Volledig lokaal.
static class Ocr
{
    static OcrEngine engine;

    public static List<Word> Read(Bitmap bmp)
    {
        // Kleine schermtekst wordt slecht herkend; vergroten helpt Windows-OCR
        // flink. Gemeten op 8pt-tabeltekst: 2x gaf 10 foute cellen, 4x nog 3.
        // Zo groot mogelijk tot 4x, binnen de maximale OCR-afmeting.
        int max = (int)OcrEngine.MaxImageDimension;
        int scale = Math.Max(1, Math.Min(4, max / Math.Max(bmp.Width, bmp.Height)));

        SoftwareBitmap soft;
        using (var work = new Bitmap(bmp.Width * scale, bmp.Height * scale, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(work))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(bmp, 0, 0, work.Width, work.Height);
            }
            // Ruwe pixels rechtstreeks aan de OCR geven (geen bestand op schijf,
            // want het kan werkdata zijn).
            BitmapData data = work.LockBits(new Rectangle(0, 0, work.Width, work.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var bytes = new byte[data.Stride * work.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            work.UnlockBits(data);
            soft = SoftwareBitmap.CreateCopyFromBuffer(CryptographicBuffer.CreateFromByteArray(bytes), BitmapPixelFormat.Bgra8, work.Width, work.Height);
        }

        // OCR in de talen van het Windows-profiel (bij Steffan: Nederlands/Engels).
        if (engine == null) engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine == null) throw new InvalidOperationException("Geen Windows-OCR-taal beschikbaar.");
        OcrResult result;
        using (soft) result = Wait(engine.RecognizeAsync(soft));

        // Alleen losse woorden + positie bewaren; de regels die Windows zelf
        // maakt negeren we, want die plakken tabelcellen aan elkaar.
        var words = new List<Word>();
        foreach (var line in result.Lines)
            foreach (var w in line.Words)
            {
                Rect r = w.BoundingRect;
                words.Add(new Word(w.Text, r.X / scale, r.Y / scale, r.Width / scale, r.Height / scale));
            }
        return words;
    }

    // WinRT-functies antwoorden "later"; hier wachten we daar gewoon op.
    static T Wait<T>(WF.IAsyncOperation<T> op)
    {
        using (var done = new ManualResetEvent(false))
        {
            op.Completed = delegate { done.Set(); };
            done.WaitOne();
        }
        if (op.Status != WF.AsyncStatus.Completed) throw new InvalidOperationException("OCR mislukt: " + op.Status);
        return op.GetResults();
    }
}

// Woorden -> tabel of tekst, en de uitvoer voor het klembord.
static class Layout
{
    class Row { public double Cy; public List<Word> Words = new List<Word>(); }

    public static string RepairSpacing(string text)
    {
        // Herstelt alleen spaties die de OCR in datums verkeerd zet; verandert
        // nooit een cijfer of letter (een gemiste ":" raden we bewust niet).
        // "15- 10-2026" -> "15-10-2026"
        text = Regex.Replace(text, @"\b(\d{1,2})- (\d{1,2}-\d{4})\b", "$1-$2");
        // "15-10-202611:14" -> "15-10-2026 11:14"
        text = Regex.Replace(text, @"\b(\d{1,2}-\d{1,2}-\d{4})(\d{1,2}:\d{2})\b", "$1 $2");
        return text;
    }

    public static Grid ToGrid(List<Word> words)
    {
        var grid = new Grid();
        if (words.Count == 0) return grid;

        // Mediaan woordhoogte = "letterhoogte"; alle drempels hieronder zijn
        // daar een veelvoud van, zodat het op elke zoom hetzelfde werkt.
        double h = words.Select(w => w.H).OrderBy(v => v).ElementAt(words.Count / 2);

        // Stap 1: rijen. Woorden waarvan het midden op dezelfde hoogte ligt
        // (binnen een halve letterhoogte) horen bij dezelfde rij.
        var rows = new List<Row>();
        foreach (var w in words.OrderBy(w => w.Y + w.H / 2))
        {
            double cy = w.Y + w.H / 2;
            Row last = rows.Count > 0 ? rows[rows.Count - 1] : null;
            if (last != null && Math.Abs(cy - last.Cy) <= h * 0.5)
            {
                last.Words.Add(w);
                last.Cy = (last.Cy * (last.Words.Count - 1) + cy) / last.Words.Count;
            }
            else
            {
                var row = new Row { Cy = cy };
                row.Words.Add(w);
                rows.Add(row);
            }
        }

        // Stap 2: kolommen. Een kolomgrens is een verticale "goot": een strook
        // van minstens 1 letterhoogte breed waar in (bijna) geen enkele rij
        // tekst staat. Gewone spaties zijn veel smaller, dus lopende tekst
        // wordt nooit per ongeluk in kolommen geknipt.
        // ponytail: 1 op de 5 rijen mag over een goot heen lopen (bv. een titel
        // boven de tabel); kleine tabellen met een brede titelregel worden dan
        // als tekst gezien. Upgraden als dat in de praktijk vaak misgaat.
        int minX = (int)Math.Floor(words.Min(w => w.X));
        int maxX = (int)Math.Ceiling(words.Max(w => w.X + w.W));
        var cover = new int[maxX - minX + 1];
        foreach (var row in rows)
        {
            var hit = new bool[cover.Length];
            foreach (var w in row.Words)
                for (int x = (int)(w.X - minX); x < (int)(w.X + w.W - minX) && x < hit.Length; x++) hit[x] = true;
            for (int x = 0; x < hit.Length; x++) if (hit[x]) cover[x]++;
        }
        int allowed = rows.Count / 5;
        var gutters = new List<double>();   // x-midden van elke goot
        int start = -1;
        for (int x = 0; x <= cover.Length; x++)
        {
            bool free = x < cover.Length && cover[x] <= allowed;
            if (free && start < 0) start = x;
            else if (!free && start >= 0)
            {
                if (x - start >= h && start > 0 && x < cover.Length) gutters.Add(minX + (start + x) / 2.0);
                start = -1;
            }
        }

        // Stap 3: elk woord in de kolom van zijn midden zetten, per cel de
        // woorden van links naar rechts aan elkaar.
        int nCols = gutters.Count + 1;
        foreach (var row in rows)
        {
            var cells = new string[nCols];
            foreach (var w in row.Words.OrderBy(w => w.X))
            {
                double mid = w.X + w.W / 2;
                int col = gutters.Count(gx => gx < mid);
                cells[col] = cells[col] == null ? w.Text : cells[col] + " " + w.Text;
            }
            grid.Rows.Add(cells.Select(c => RepairSpacing(c ?? "")).ToArray());
        }

        // Tabel = minstens 2 kolommen en minstens 2 rijen met meer dan 1 gevulde cel.
        int multi = grid.Rows.Count(r => r.Count(c => c != "") >= 2);
        grid.IsTable = nCols >= 2 && multi >= 2;
        if (!grid.IsTable)
            // Geen tabel: elke rij is gewoon een regel tekst.
            grid.Rows = grid.Rows.Select(r => new[] { string.Join(" ", r.Where(c => c != "")) }).ToList();
        return grid;
    }

    public static string PlainText(Grid grid)
    {
        if (!grid.IsTable) return string.Join("\r\n", grid.Rows.Select(r => r[0]));

        // Kolommen uitlijnen met spaties. Tussen ``` zodat Teams, Slack en
        // WhatsApp het in een vaste-breedte-letter tonen.
        int n = grid.Rows[0].Length;
        var widths = Enumerable.Range(0, n).Select(c => grid.Rows.Max(r => r[c].Length)).ToArray();
        var lines = grid.Rows.Select(r => string.Join("  ", Enumerable.Range(0, n).Select(c => r[c].PadRight(widths[c]))).TrimEnd());
        return "```\r\n" + string.Join("\r\n", lines) + "\r\n```";
    }

    public static string Html(Grid grid)
    {
        if (!grid.IsTable) return string.Join("<br>", grid.Rows.Select(r => System.Net.WebUtility.HtmlEncode(r[0])));
        const string td = "<td style=\"border:1px solid #999;padding:2px 6px\">";
        var trs = grid.Rows.Select(r => "<tr>" + string.Concat(r.Select(c => td + System.Net.WebUtility.HtmlEncode(c) + "</td>")) + "</tr>");
        return "<table style=\"border-collapse:collapse\">" + string.Concat(trs) + "</table>";
    }

    public static void SetClipboard(string plain, string html)
    {
        // Twee versies tegelijk; het programma waarin je plakt kiest zelf:
        // Gmail/Excel nemen de HTML-tabel, chat-apps de platte tekst.
        // HTML op het klembord vraagt een vaste kop met byte-posities (CF_HTML).
        const string pre = "<html><body><!--StartFragment-->";
        const string post = "<!--EndFragment--></body></html>";
        const string header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        var utf8 = Encoding.UTF8;
        int hLen = utf8.GetByteCount(string.Format(header, 0, 0, 0, 0));
        int startFrag = hLen + utf8.GetByteCount(pre);
        int endFrag = startFrag + utf8.GetByteCount(html);
        int endHtml = endFrag + utf8.GetByteCount(post);

        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, plain);
        data.SetData(DataFormats.Html, string.Format(header, hLen, endHtml, startFrag, endFrag) + pre + html + post);
        Clipboard.SetDataObject(data, true);
    }
}

// Controles: "DataPlakker.exe --selftest". Overschrijft het klembord.
static class SelfTest
{
    static int failed;

    static void Assert(bool cond, string msg)
    {
        Console.WriteLine((cond ? "OK  " : "MISLUKT  ") + msg);
        if (!cond) failed++;
    }

    static Word W(string t, double x, double y) { return new Word(t, x, y, 10 * t.Length, 14); }

    // Tekent regels tekst op een witte afbeelding; elke regel is een lijst van
    // (x-positie, tekst), zodat we kolommen kunnen nabootsen.
    static Bitmap Image(params Tuple<int, string>[][] lines)
    {
        var bmp = new Bitmap(700, 40 + 26 * lines.Length, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        using (var font = new Font("Segoe UI", 11))
        {
            g.Clear(Color.White);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            for (int i = 0; i < lines.Length; i++)
                foreach (var cell in lines[i]) g.DrawString(cell.Item2, font, Brushes.Black, cell.Item1, 20 + 26 * i);
        }
        return bmp;
    }

    static Tuple<int, string> C(int x, string t) { return Tuple.Create(x, t); }

    public static int Run()
    {
        // 1. Tabelherkenning zonder OCR
        var grid = Layout.ToGrid(new List<Word> { W("Artikel", 0, 0), W("Aantal", 200, 0), W("Zwembad", 0, 30), W("pomp", 75, 30), W("12", 200, 30), W("Filter", 0, 60), W("3", 200, 60) });
        Assert(grid.IsTable, "nagemaakte tabel wordt als tabel herkend");
        Assert(grid.Rows.Count == 3 && grid.Rows[0].Length == 2, "nagemaakte tabel heeft 3 rijen x 2 kolommen");
        Assert(grid.Rows[1][0] == "Zwembad pomp" && grid.Rows[1][1] == "12", "woorden met gewone spatie blijven in dezelfde cel");
        Assert(Layout.PlainText(grid) == "```\r\nArtikel       Aantal\r\nZwembad pomp  12\r\nFilter        3\r\n```", "tabel als uitgelijnde tekst tussen ``` voor chat");
        Assert(Layout.RepairSpacing("15- 10-2026") == "15-10-2026", "spatieherstel: losse spatie in datum weg");
        Assert(Layout.RepairSpacing("15-10-202611:14") == "15-10-2026 11:14", "spatieherstel: datum en tijd weer gescheiden");
        Assert(Layout.RepairSpacing("ART-00012345 10-2026") == "ART-00012345 10-2026", "spatieherstel: laat andere tekst ongemoeid");

        grid = Layout.ToGrid(new List<Word> { W("Dit", 0, 0), W("is", 40, 0), W("tekst", 65, 0), W("nog", 0, 30), W("een", 40, 30), W("regel", 85, 30) });
        Assert(!grid.IsTable, "lopende tekst wordt niet als tabel gezien");
        Assert(Layout.PlainText(grid) == "Dit is tekst\r\nnog een regel", "lopende tekst blijft per regel behouden");

        // 2. Echte OCR
        using (var bmp = Image(
            new[] { C(10, "Artikel"), C(260, "Leverancier"), C(480, "Aantal") },
            new[] { C(10, "Zwembadpomp 3000"), C(260, "Bestway"), C(480, "12") },
            new[] { C(10, "Filterpatroon"), C(260, "Intex"), C(480, "240") },
            new[] { C(10, "Solarzeil groot"), C(260, "Aqua Blauw"), C(480, "7") }))
            grid = Layout.ToGrid(Ocr.Read(bmp));
        Console.WriteLine(Layout.PlainText(grid));
        Assert(grid.IsTable, "OCR: getekende tabel wordt als tabel herkend");
        Assert(grid.Rows.Count == 4 && grid.Rows[0].Length == 3, "OCR: 4 rijen x 3 kolommen (kreeg " + grid.Rows.Count + " x " + grid.Rows[0].Length + ")");
        Assert(grid.Rows.Count == 4 && grid.Rows[0].Length == 3 && grid.Rows[3][1] == "Aqua Blauw" && grid.Rows[2][2] == "240", "OCR: celinhoud klopt");

        using (var bmp = Image(
            new[] { C(10, "De levering van vandaag mist twee dozen filterpatronen.") },
            new[] { C(10, "Graag een nieuwe zending of een creditnota.") }))
            grid = Layout.ToGrid(Ocr.Read(bmp));
        Assert(!grid.IsTable, "OCR: lopende tekst wordt niet als tabel gezien");
        Assert(Layout.PlainText(grid).StartsWith("De levering van vandaag mist twee dozen"), "OCR: tekst klopt");

        // 3. Klembord
        grid = new Grid { IsTable = true };
        grid.Rows.Add(new[] { "a & b", "c" });
        Layout.SetClipboard(Layout.PlainText(grid), Layout.Html(grid));
        IDataObject back = Clipboard.GetDataObject();
        Assert(((string)back.GetData(DataFormats.UnicodeText)).Contains("a & b  c"), "klembord: platte tekst staat erop");
        var cf = (string)back.GetData(DataFormats.Html);
        int s = int.Parse(Regex.Match(cf, @"StartFragment:(\d+)").Groups[1].Value);
        int e = int.Parse(Regex.Match(cf, @"EndFragment:(\d+)").Groups[1].Value);
        string frag = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(cf), s, e - s);
        Assert(frag.StartsWith("<table") && frag.Contains("a &amp; b") && frag.EndsWith("</table>"), "klembord: HTML-fragment staat op de juiste byte-posities");

        Console.WriteLine(failed == 0 ? "\nAlle controles geslaagd." : "\n" + failed + " controle(s) mislukt.");
        return failed == 0 ? 0 : 1;
    }
}
