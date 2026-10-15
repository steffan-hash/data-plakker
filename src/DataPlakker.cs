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

        // "--ocr <afbeelding>": leest een opgeslagen screenshot uit en schrijft
        // de cellen tab-gescheiden naar de uitvoer. Om de herkenning te meten
        // op echte schermen, zonder selectiekader.
        if (args.Length == 2 && args[0] == "--ocr")
        {
            using (var outp = new System.IO.StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)))
            using (var bmp = new Bitmap(args[1]))
            using (var argb = bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format32bppArgb))
                foreach (var row in Layout.ToGrid(Ocr.Read(argb)).Rows) outp.WriteLine(string.Join("\t", row));
            return 0;
        }

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
    public List<string> Readings;   // alle verschillende lezingen uit het herlezen (null = niet herlezen)
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
    // OCR in de talen van het Windows-profiel. Gemeten: Engels leest codes en
    // gemengde tekst beter dan Nederlands, ook bij Nederlandse woorden.
    // Eén motor per thread: één motor kan niet door meerdere threads tegelijk.
    static readonly ThreadLocal<OcrEngine> engine = new ThreadLocal<OcrEngine>(() => OcrEngine.TryCreateFromUserProfileLanguages());

    // Vergrotingen waarop elk woord nog eens los gelezen wordt (zie Reread).
    static readonly int[] RereadScales = { 3, 6, 8 };

    public static List<Word> Read(Bitmap bmp)
    {
        if (engine.Value == null) throw new InvalidOperationException("Geen Windows-OCR-taal beschikbaar.");
        var words = ReadOnce(bmp, 4);
        // Ook een los gelezen stuk kan weer een woord missen; dan nog een ronde.
        for (int round = 0; round < 3 && RecoverMissed(bmp, words); round++) { }
        Reread(bmp, words);
        RestoreSpaces(bmp, words);
        return words;
    }

    // Windows-OCR laat soms een heel woord weg, afhankelijk van waar het kader
    // precies valt (gemeten: een locatiecode, "XXL", "UV-C lamp TL 30W").
    // Daarom zoeken we tekst die buiten alle gevonden woorden valt: stukjes
    // inkt van tekstformaat, per regel samengevoegd tot woorden, en lezen die
    // los. Lijnen en randen vallen af op hun vorm.
    internal static bool RecoverMissed(Bitmap bmp, List<Word> words)
    {
        if (words.Count == 0) return false;
        double h = words.Select(w => w.H).OrderBy(x => x).ElementAt(words.Count / 2);
        int width = bmp.Width, height = bmp.Height, stride;
        byte[] px = Pixels(bmp, out stride);

        var covered = new bool[width * height];
        int pad = Math.Max(2, (int)(0.3 * h));
        foreach (var w in words)
            for (int y = Math.Max(0, (int)w.Y - pad); y < Math.Min(height, (int)(w.Y + w.H) + pad); y++)
                for (int x = Math.Max(0, (int)w.X - pad); x < Math.Min(width, (int)(w.X + w.W) + pad); x++)
                    covered[y * width + x] = true;

        // Achtergrond per pixelregel = de meest voorkomende helderheid op die regel.
        var ink = new bool[width * height];
        var hist = new int[256];
        for (int y = 0; y < height; y++)
        {
            Array.Clear(hist, 0, 256);
            for (int x = 0; x < width; x++) hist[Lum(px, stride, x, y)]++;
            int bg = Array.IndexOf(hist, hist.Max());
            for (int x = 0; x < width; x++)
                ink[y * width + x] = !covered[y * width + x] && Math.Abs(Lum(px, stride, x, y) - bg) > 60;
        }

        // Losse inktvlekken (aaneengesloten pixels) met hun omhullende rechthoek.
        var blobs = new List<Rectangle>();
        var stack = new Stack<int>();
        for (int start = 0; start < ink.Length; start++)
        {
            if (!ink[start]) continue;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = 0, y1 = 0;
            ink[start] = false; stack.Push(start);
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % width, y = i / width;
                x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height || !ink[ny * width + nx]) continue;
                        ink[ny * width + nx] = false; stack.Push(ny * width + nx);
                    }
            }
            var b = Rectangle.FromLTRB(x0, y0, x1 + 1, y1 + 1);
            // Hoger dan een letter, of lang en plat: een kader- of rasterlijn.
            // Kleine vlekjes blijven: een streepje of punt hoort vaak bij een woord ("109-04"),
            // en kleine cijfers raken elkaar vaak, dus breed mag.
            if (b.Height <= 1.6 * h && !(b.Height <= 0.25 * h && b.Width > 2 * h)) blobs.Add(b);
        }

        // Vlekken op dezelfde regel met hooguit een spatie ertussen = één woord.
        bool merged = true;
        while (merged)
        {
            merged = false;
            for (int i = 0; i < blobs.Count && !merged; i++)
                for (int j = i + 1; j < blobs.Count && !merged; j++)
                {
                    Rectangle a = blobs[i], c = blobs[j];
                    int overlap = Math.Min(a.Bottom, c.Bottom) - Math.Max(a.Top, c.Top);
                    int gap = Math.Max(a.Left, c.Left) - Math.Min(a.Right, c.Right);
                    if (overlap < 0.3 * Math.Min(a.Height, c.Height) || gap > 0.5 * h) continue;
                    blobs[i] = Rectangle.Union(a, c); blobs.RemoveAt(j); merged = true;
                }
        }

        int known = words.Count;
        foreach (var b in blobs)
        {
            if (b.Height < 0.5 * h) continue;   // los streepje of puntje
            Point o;
            // Ruimer knippen: de rand van de gevonden woorden telde niet als inkt, dus
            // een eerste of laatste letter kan half buiten de vlek vallen. Wat daardoor
            // dubbel gelezen wordt, valt hieronder af.
            using (var tile = Isolate(bmp, new Word("", b.X - pad, b.Y, b.Width - 1 + 2 * pad, b.Height - 1), out o))
            {
                if (tile == null) continue;
                // Een heel kort woord ("XXL") leest Windows pas vanaf ~6x vergroot.
                var found = ReadOnce(tile, 4);
                if (found.Count == 0) found = ReadOnce(tile, 8);
                foreach (var w in found)
                {
                    // Eén los teken zonder cijfer is meestal een icoontje (bv. een sorteerpijltje).
                    if (!w.Text.Any(char.IsLetterOrDigit) || (w.Text.Length < 2 && !w.Text.Any(char.IsDigit))) continue;
                    w.X += o.X; w.Y += o.Y;
                    double cx = w.X + w.W / 2, cy = w.Y + w.H / 2;
                    if (words.Take(known).Any(k => cx >= k.X && cx <= k.X + k.W && cy >= k.Y && cy <= k.Y + k.H)) continue;
                    words.Add(w);
                }
            }
        }
        return words.Count > known;
    }

    static byte[] Pixels(Bitmap bmp, out int stride)
    {
        BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        stride = data.Stride;
        var px = new byte[stride * bmp.Height];
        Marshal.Copy(data.Scan0, px, 0, px.Length);
        bmp.UnlockBits(data);
        return px;
    }

    // Windows-OCR plakt woorden met een smalle spatie aan elkaar ("T5 40W" ->
    // "T540W"). Een helemaal leeg stuk van minstens 0,16 letterhoogte komt
    // binnen een woord bijna niet voor (gemeten: spaties in het WMS 0,17-0,34,
    // gaten binnen woorden tot 0,15; alleen naast een "1" breder), tussen
    // woorden wel. Op zo'n plek lezen we alles rechts ervan los; is dat precies
    // het eind van het woord, dan komt daar een spatie. Tekens veranderen nooit.
    static void RestoreSpaces(Bitmap bmp, List<Word> words)
    {
        int stride;
        byte[] px = Pixels(bmp, out stride);
        foreach (var w in words)
        {
            if (w.Text.Contains('_') || w.Text.Contains(' ')) continue;   // underscore-gat is geen spatie
            int x0 = Math.Max(0, (int)Math.Floor(w.X)), x1 = Math.Min(bmp.Width - 1, (int)Math.Ceiling(w.X + w.W));
            int y0 = Math.Max(0, (int)Math.Floor(w.Y)), y1 = Math.Min(bmp.Height - 1, (int)Math.Ceiling(w.Y + w.H));
            int bgAt = Math.Max(0, y0 - 1) * stride + Math.Max(0, x0 - 1) * 4;
            var cuts = new List<int>();   // begin van de inkt na elk leeg stuk
            int run = 0; bool seenInk = false;
            for (int x = x0; x <= x1; x++)
            {
                // Per kleurkanaal vergelijken: de gekleurde randjes van ClearType-tekst
                // hebben bijna dezelfde helderheid als de achtergrond, maar zijn wel inkt.
                bool ink = false;
                for (int y = y0; y <= y1 && !ink; y++)
                    for (int ch = 0; ch < 3 && !ink; ch++) ink = Math.Abs(px[y * stride + x * 4 + ch] - px[bgAt + ch]) > 13;
                if (ink) { if (seenInk && run >= Math.Max(2, 0.16 * w.H)) cuts.Add(x); run = 0; seenInk = true; }
                else run++;
            }
            var spaces = new List<int>();   // tekenposities waar een spatie hoort
            foreach (int cut in cuts)
                using (var tile = Isolate(bmp, new Word("", cut - 1, w.Y, x1 - cut + 1, w.H)))
                {
                    string right = tile == null ? "" : string.Concat(ReadOnce(tile, 4).Select(x => x.Text));
                    int at = w.Text.Length - right.Length;
                    if (right.Length > 0 && at > 0 &&
                        string.Equals(right, w.Text.Substring(at), StringComparison.OrdinalIgnoreCase) &&
                        w.Text[at - 1] != '1' && w.Text[at] != '1')   // een "1" heeft vaak extra ruimte ("16-2", "12")
                        spaces.Add(at);
                }
            foreach (int at in spaces.Distinct().OrderByDescending(i => i)) w.Text = w.Text.Insert(at, " ");
        }
    }

    // Windows-OCR gokt een woord mede op basis van de tekst eromheen, en gokt
    // in tabellen met codes soms mis ("16-2-A-118-05" -> "16-24-118-05",
    // "zwembad" -> "muembad"). Los gelezen, zonder buren, gaat dat vaak wél
    // goed. Daarom wordt elk woord nog 3x los gelezen (precies uitgeknipt, met
    // lege achtergrond eromheen) en wint de lezing die het vaakst voorkomt:
    // minstens 2 van de 4 en geen gelijkspel, anders blijft de eerste lezing.
    // Verdeeld over alle processorkernen, anders duurt het te lang.
    static void Reread(Bitmap bmp, List<Word> words)
    {
        // Alleen woorden met een cijfer erin (codes, maten, aantallen) en korte
        // woorden in hoofdletters ("UVC" -> "UV-C", "SSE" -> "SS25"): bij gewone
        // woorden helpt de context juist (gemeten: verdwenen underscores), en het
        // scheelt veel tijd.
        var tiles = words.Select(w => w.Text.Any(char.IsDigit) || Regex.IsMatch(w.Text, "^[A-Z-]{2,5}$") ? Isolate(bmp, w) : null).ToList();
        var reads = new string[words.Count, RereadScales.Length];
        // Per woord één thread: een plaatje kan niet door twee threads tegelijk gelezen worden.
        System.Threading.Tasks.Parallel.For(0, words.Count, w =>
        {
            if (tiles[w] == null) return;
            for (int s = 0; s < RereadScales.Length; s++)
                reads[w, s] = string.Join(" ", ReadOnce(tiles[w], RereadScales[s]).Select(x => x.Text));
        });
        for (int w = 0; w < words.Count; w++)
        {
            if (tiles[w] == null) continue;
            tiles[w].Dispose();
            var votes = new List<string> { words[w].Text };
            for (int s = 0; s < RereadScales.Length; s++) if (!string.IsNullOrEmpty(reads[w, s])) votes.Add(reads[w, s]);
            // Verschil alleen in hoofd-/kleine letters telt niet als andere lezing.
            var ranked = votes.GroupBy(v => v.ToUpperInvariant()).OrderByDescending(g => g.Count()).ToList();
            bool clearWinner = ranked[0].Count() >= 2 && (ranked.Count == 1 || ranked[1].Count() < ranked[0].Count());
            if (clearWinner && ranked[0].Key != words[w].Text.ToUpperInvariant())
                // Los gelezen komt een code soms in kleine letters terug ("uv-c");
                // was het eerst helemaal hoofdletters, dan blijft dat zo.
                words[w].Text = words[w].Text == words[w].Text.ToUpperInvariant()
                    ? ranked[0].Key
                    : ranked[0].GroupBy(v => v).OrderByDescending(g => g.Count()).First().Key;
            // Bij twijfel kan de kolom later nog kiezen (zie RepairToColumnPattern).
            words[w].Readings = votes.Distinct().ToList();
        }
    }

    // Eén woord precies uitknippen en met een rand achtergrondkleur eromheen
    // op een eigen plaatje zetten, zodat er geen letters van de buren meekomen.
    static Bitmap Isolate(Bitmap src, Word w)
    {
        Point origin;
        return Isolate(src, w, out origin);
    }

    // origin = waar linksboven van het nieuwe plaatje in de bron zou liggen.
    static Bitmap Isolate(Bitmap src, Word w, out Point origin)
    {
        var r = Rectangle.Intersect(
            Rectangle.FromLTRB((int)Math.Floor(w.X) - 1, (int)Math.Floor(w.Y) - 1, (int)Math.Ceiling(w.X + w.W) + 1, (int)Math.Ceiling(w.Y + w.H) + 1),
            new Rectangle(0, 0, src.Width, src.Height));
        int m = Math.Max(4, r.Height / 2);
        origin = new Point(r.Left - m, r.Top - m);
        if (r.Width < 3 || r.Height < 3) return null;
        var tile = new Bitmap(r.Width + 2 * m, r.Height + 2 * m, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(tile))
        {
            // Achtergrond = meest voorkomende kleur in het stuk, niet het hoekje erbuiten:
            // bij een gekleurde markering achter de tekst is dat hoekje wit, en dan
            // vallen letters die de rand van de markering raken weg.
            var counts = new Dictionary<int, int>();
            for (int y = r.Top; y < r.Bottom; y++)
                for (int x = r.Left; x < r.Right; x++)
                {
                    int c = src.GetPixel(x, y).ToArgb(), n;
                    counts.TryGetValue(c, out n);
                    counts[c] = n + 1;
                }
            g.Clear(Color.FromArgb(counts.OrderByDescending(kv => kv.Value).First().Key));
            g.DrawImage(src, new Rectangle(m, m, r.Width, r.Height), r, GraphicsUnit.Pixel);
        }
        return tile;
    }

    static List<Word> ReadOnce(Bitmap bmp, int maxScale)
    {
        // Kleine schermtekst wordt slecht herkend; vergroten helpt Windows-OCR
        // flink. Gemeten op 8pt-tabeltekst: 2x gaf 10 foute cellen, 4x nog 3.
        // Op een echte WMS-screenshot scoorde 4x ook beter dan 3x en 5x, en
        // zacht vergroten (bicubic) veel beter dan scherp of zwart-wit.
        // Zo groot mogelijk tot maxScale, binnen de maximale OCR-afmeting.
        int max = (int)OcrEngine.MaxImageDimension;
        int scale = Math.Max(1, Math.Min(maxScale, max / Math.Max(bmp.Width, bmp.Height)));

        SoftwareBitmap soft;
        byte[] px;
        int stride, width, height;
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
            stride = data.Stride; width = work.Width; height = work.Height;
            px = new byte[stride * height];
            Marshal.Copy(data.Scan0, px, 0, px.Length);
            work.UnlockBits(data);
            soft = SoftwareBitmap.CreateCopyFromBuffer(CryptographicBuffer.CreateFromByteArray(px), BitmapPixelFormat.Bgra8, width, height);
        }

        OcrResult result;
        using (soft) result = Wait(engine.Value.RecognizeAsync(soft));

        // Alleen losse woorden + positie bewaren; de regels die Windows zelf
        // maakt negeren we, want die plakken tabelcellen aan elkaar.
        var words = new List<Word>();
        foreach (var line in result.Lines)
        {
            Word prev = null;
            foreach (var w in line.Words)
            {
                Rect r = w.BoundingRect;
                var cur = new Word(w.Text, r.X, r.Y, r.Width, r.Height);
                // Windows-OCR laat underscores weg ("ZONE_A" -> "ZONE" "A");
                // dan plakken we de woorden hier weer aan elkaar.
                if (prev != null && UnderscoreBetween(px, stride, width, height, prev, cur))
                {
                    double right = cur.X + cur.W, bottom = Math.Max(prev.Y + prev.H, cur.Y + cur.H);
                    prev.Text += "_" + cur.Text;
                    prev.Y = Math.Min(prev.Y, cur.Y);
                    prev.W = right - prev.X;
                    prev.H = bottom - prev.Y;
                    continue;
                }
                words.Add(cur);
                prev = cur;
            }
        }
        foreach (var w in words) { w.X /= scale; w.Y /= scale; w.W /= scale; w.H /= scale; }
        return words;
    }

    // Staat er onderaan het gat tussen twee woorden een liggende streep? Dan
    // was het een underscore. Loopt diezelfde streep ook onder het woord ervoor
    // door, dan is het onderstreping (bv. een link) en blijft het een spatie.
    static bool UnderscoreBetween(byte[] px, int stride, int width, int height, Word a, Word b)
    {
        int x0 = (int)(a.X + a.W) + 1, x1 = (int)b.X - 1;
        if (x1 - x0 < 2) return false;
        double h = Math.Max(a.H, b.H), bottom = Math.Max(a.Y + a.H, b.Y + b.H);
        int yBg = (int)(bottom - 0.6 * h);
        if (yBg < 0) return false;
        int bg = Lum(px, stride, (x0 + x1) / 2, yBg);   // achtergrondkleur midden in het gat
        for (int y = Math.Max(0, (int)(bottom - 0.3 * h)); y <= Math.Min(height - 1, (int)(bottom + 0.4 * h)); y++)
            if (InkFraction(px, stride, x0, x1, y, bg) >= 0.8 &&
                InkFraction(px, stride, (int)a.X, (int)(a.X + a.W), y, bg) < 0.6)
                return true;
        return false;
    }

    // Deel van de pixels op regel y (van x0 t/m x1) dat duidelijk afwijkt van de achtergrond.
    static double InkFraction(byte[] px, int stride, int x0, int x1, int y, int bg)
    {
        int ink = 0;
        for (int x = x0; x <= x1; x++) if (Math.Abs(Lum(px, stride, x, y) - bg) > 60) ink++;
        return (double)ink / (x1 - x0 + 1);
    }

    static int Lum(byte[] px, int stride, int x, int y)
    {
        int i = y * stride + x * 4;   // pixels staan als blauw, groen, rood, alfa
        return (114 * px[i] + 587 * px[i + 1] + 299 * px[i + 2]) / 1000;
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

    // Minimale breedte van een strenge goot, in letterhoogtes (zie stap 2).
    // Op de WMS-meting gaven 0,6 en 0,8 hetzelfde resultaat; 0,8 houdt meer
    // afstand tot gewone spaties (~0,3) in lopende tekst.
    const double StrictGap = 0.8;

    // Stroken (begin, eind) waar hooguit "allowed" rijen tekst hebben en die
    // minstens "minWidth" breed zijn. Stroken aan de rand tellen niet mee.
    static List<Tuple<int, int>> FreeRuns(int[] cover, int allowed, double minWidth)
    {
        var runs = new List<Tuple<int, int>>();
        int start = -1;
        for (int x = 0; x <= cover.Length; x++)
        {
            bool free = x < cover.Length && cover[x] <= allowed;
            if (free && start < 0) start = x;
            else if (!free && start >= 0)
            {
                if (x - start >= minWidth && start > 0 && x < cover.Length) runs.Add(Tuple.Create(start, x));
                start = -1;
            }
        }
        return runs;
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
        // waar geen tekst staat. Gewone spaties zijn veel smaller, dus lopende
        // tekst wordt nooit per ongeluk in kolommen geknipt. Twee soorten:
        // - streng: in GEEN enkele rij tekst, minstens StrictGap letterhoogte
        //   breed. Vangt ook lege kolommen waar alleen een kop boven staat.
        // - ruim: in hooguit 1 op de 5 rijen tekst, minstens 1 letterhoogte.
        //   Vangt kolommen waar afgekapte tekst ("...") of een titel overheen
        //   loopt. Alleen gebruikt waar geen strenge goot ligt.
        // ponytail: kleine tabellen (< 5 rijen) met een brede titelregel worden
        // nog als tekst gezien. Upgraden als dat in de praktijk vaak misgaat.
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
        var strict = FreeRuns(cover, 0, h * StrictGap);
        var gutters = strict
            .Concat(FreeRuns(cover, rows.Count / 5, h).Where(l => !strict.Any(s => s.Item1 < l.Item2 && l.Item1 < s.Item2)))
            .Select(r => minX + (r.Item1 + r.Item2) / 2.0)   // x-midden van elke goot
            .OrderBy(x => x).ToList();

        // Stap 3: elk woord in de kolom waar het BEGINT (linkerrand), zodat
        // afgekapte tekst die tot in de volgende kolom doorloopt bij zijn eigen
        // kolom blijft. Per cel de woorden van links naar rechts aan elkaar.
        int nCols = gutters.Count + 1;
        var readings = new List<List<string>[]>();   // per cel met precies één herlezen woord: diens lezingen
        foreach (var row in rows)
        {
            var cells = new string[nCols];
            var cellWords = new List<Word>[nCols];
            foreach (var w in row.Words.OrderBy(w => w.X))
            {
                int col = gutters.Count(gx => gx < w.X);
                cells[col] = cells[col] == null ? w.Text : cells[col] + " " + w.Text;
                (cellWords[col] = cellWords[col] ?? new List<Word>()).Add(w);
            }
            grid.Rows.Add(cells.Select(c => RepairSpacing(c ?? "")).ToArray());
            readings.Add(cellWords.Select(ws => ws != null && ws.Count == 1 && ws[0].Readings != null
                ? ws[0].Readings.Select(RepairSpacing).ToList() : null).ToArray());
        }

        // Tabel = minstens 2 kolommen en minstens 2 rijen met meer dan 1 gevulde cel.
        int multi = grid.Rows.Count(r => r.Count(c => c != "") >= 2);
        grid.IsTable = nCols >= 2 && multi >= 2;
        if (!grid.IsTable)
            // Geen tabel: elke rij is gewoon een regel tekst.
            grid.Rows = grid.Rows.Select(r => new[] { string.Join(" ", r.Where(c => c != "")) }).ToList();
        else
            for (int c = 0; c < nCols; c++) RepairToColumnPattern(grid.Rows, readings, c);
        return grid;
    }

    // Vorm van een cel: elk cijfer wordt 9, elke letter A, de rest blijft.
    // "Y0010" -> "A9999", "16-2-A-116-00" -> "99-9-A-999-99".
    static string Shape(string s)
    {
        return new string(s.Select(ch => char.IsDigit(ch) ? '9' : char.IsLetter(ch) ? 'A' : ch).ToArray());
    }

    // Codes recht trekken op het patroon van hun kolom. Een "patroon" is een
    // vorm die minstens 3 keer in de kolom voorkomt. Twee reparaties:
    // 1. Past een cel precies op een patroon zodra je O/o als 0 en I/l/| als 1
    //    leest (alleen waar het patroon een cijfer heeft), dan was het een
    //    leesfout: "YOOIO" -> "Y0010".
    // 2. Twijfelde het herlezen (gelijkspel), dan wint de lezing die past bij
    //    het patroon dat het vaakst in de kolom voorkomt. Gemeten: korte
    //    locatiecodes werden 2 tegen 2 gelezen als "16-24-116-00" en
    //    "16-2-A-116-OO"; de kolom heeft meer codes van die tweede vorm.
    // Andere letters raken we nooit aan, en we kiezen alleen uit lezingen die
    // de OCR zelf gaf. Vrije tekst heeft geen terugkerende vorm.
    static void RepairToColumnPattern(List<string[]> rows, List<List<string>[]> readings, int c)
    {
        var counts = rows.Select(r => Shape(r[c])).Where(s => s.Contains('9'))
            .GroupBy(s => s).Where(g => g.Count() >= 3).ToDictionary(g => g.Key, g => g.Count());
        if (counts.Count == 0) return;
        for (int i = 0; i < rows.Count; i++)
        {
            string best = null;
            int bestCount = 0;
            var candidates = new List<string> { rows[i][c] };
            if (readings[i][c] != null) candidates.AddRange(readings[i][c]);
            foreach (var cand in candidates)
            {
                string fit = FitToPattern(cand, counts.Keys);
                if (fit != null && counts[Shape(fit)] > bestCount) { best = fit; bestCount = counts[Shape(fit)]; }
            }
            if (best != null) rows[i][c] = best;
        }
    }

    // De cel zelf als die al op een patroon past, anders de O->0 / I->1-versie
    // die er precies op past, anders null.
    static string FitToPattern(string cell, IEnumerable<string> patterns)
    {
        if (cell == "") return null;
        if (patterns.Contains(Shape(cell))) return cell;
        foreach (var t in patterns.Where(t => t.Length == cell.Length))
        {
            var fixedCell = new string(cell.Select((ch, i) =>
                t[i] != '9' ? ch : "Oo".IndexOf(ch) >= 0 ? '0' : "Il|".IndexOf(ch) >= 0 ? '1' : ch).ToArray());
            if (Shape(fixedCell) == t) return fixedCell;
        }
        return null;
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

    // Eén regel tekst in een bepaalde stijl (bv. onderstreept), voor de underscore-controles.
    static Bitmap Line(string text, FontStyle style)
    {
        var bmp = new Bitmap(500, 60, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        using (var font = new Font("Segoe UI", 11, style))
        {
            g.Clear(Color.White);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.DrawString(text, font, Brushes.Black, 10, 20);
        }
        return bmp;
    }

    static string ReadLine(Bitmap bmp)
    {
        using (bmp) return string.Join(" ", Ocr.Read(bmp).Select(w => w.Text));
    }

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

        // Lege kolom met alleen een kop (zoals "Urn" in het WMS) blijft een eigen kolom.
        grid = Layout.ToGrid(new List<Word> {
            W("Code", 0, 0), W("Leeg", 100, 0), W("Zone", 200, 0),
            W("Y0002", 0, 30), W("Ugly", 200, 30), W("Y0003", 0, 60), W("Ugly", 200, 60), W("Y0004", 0, 90), W("Shelves", 200, 90) });
        Assert(grid.Rows[0].Length == 3 && grid.Rows[0][1] == "Leeg" && grid.Rows[1][1] == "" && grid.Rows[1][2] == "Ugly", "lege kolom met alleen een kop blijft een eigen kolom");

        // Codes worden rechtgetrokken op het patroon van hun kolom (O->0, I->1).
        grid = Layout.ToGrid(new List<Word> {
            W("Y0002", 0, 0), W("a", 200, 0), W("Y0003", 0, 30), W("b", 200, 30), W("Y0004", 0, 60), W("c", 200, 60),
            W("YOOIO", 0, 90), W("Olie", 200, 90) });
        Assert(grid.Rows[3][0] == "Y0010", "kolompatroon: YOOIO wordt Y0010");
        Assert(grid.Rows[3][1] == "Olie", "kolompatroon: gewone tekst blijft ongemoeid");

        // Bij twijfel kiest de kolom: van de lezingen wint die op het patroon van de kolom past.
        var twijfel = W("5-4-14", 0, 120);
        twijfel.Readings = new List<string> { "5-4-14", "5-A-14" };
        grid = Layout.ToGrid(new List<Word> {
            W("1-A-10", 0, 0), W("a", 200, 0), W("2-A-11", 0, 30), W("b", 200, 30), W("3-A-12", 0, 60), W("c", 200, 60),
            W("4-A-13", 0, 90), W("d", 200, 90), twijfel, W("e", 200, 120) });
        Assert(grid.Rows[4][0] == "5-A-14", "kolompatroon: bij twijfel wint de lezing die bij de kolom past");

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

        string us = ReadLine(Line("ZONE_A_LEGBORD_GROOT", FontStyle.Regular));
        Assert(us == "ZONE_A_LEGBORD_GROOT", "OCR: underscores blijven staan (kreeg \"" + us + "\")");
        string ul = ReadLine(Line("Bekijk de levering hier", FontStyle.Underline));
        Assert(!ul.Contains("_"), "OCR: onderstreepte tekst krijgt geen underscores (kreeg \"" + ul + "\")");

        // Smalle spatie (zoals in het WMS) blijft een spatie, gewone woorden blijven heel.
        using (var bmp = new Bitmap(300, 40, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font("Segoe UI", 8))
            {
                g.Clear(Color.White);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.DrawString("Philips lamp T5", font, Brushes.Black, 10, 12, StringFormat.GenericTypographic);
                float w = g.MeasureString("Philips lamp T5", font, PointF.Empty, StringFormat.GenericTypographic).Width;
                g.DrawString("40W Pizzaborstel", font, Brushes.Black, 10 + w + 3, 12, StringFormat.GenericTypographic);
            }
            string sp = ReadLine((Bitmap)bmp.Clone());
            Assert(sp.Contains("T5 40W") && sp.Contains("Pizzaborstel"), "OCR: smalle spatie hersteld, woord heel (kreeg \"" + sp + "\")");
        }

        // Een woord dat de OCR mist, wordt alsnog los gelezen.
        using (var bmp = Image(new[] { C(10, "Zwembadpomp"), C(260, "16-2-A-109-04-A") }))
        {
            var words = Ocr.Read(bmp);
            words.RemoveAll(w => w.Text.Contains("109"));
            Ocr.RecoverMissed(bmp, words);
            Assert(words.Any(w => w.Text == "16-2-A-109-04-A"), "OCR: gemist woord wordt teruggevonden (kreeg \"" + string.Join(" ", words.Select(w => w.Text)) + "\")");
        }

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
