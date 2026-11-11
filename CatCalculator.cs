using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CatCalculator
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main(string[] args)
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--capture")
            {
                using (CalculatorForm form = new CalculatorForm())
                using (Bitmap image = new Bitmap(342, 421))
                {
                    form.Refresh();
                    form.DrawToBitmap(image, new Rectangle(0, 0, 342, 421));
                    image.Save(args[1], ImageFormat.Png);
                }
                return;
            }
            Application.Run(new CalculatorForm());
        }
    }

    internal sealed class CalculatorForm : Form
    {
        private const int WindowWidth = 342;
        private const int WindowHeight = 421;
        private const int GridX = 23;
        private const int ColWidth = 53;
        private const int Gap = 5;
        private const int ButtonHeight = 34;

        private readonly Stopwatch clock = new Stopwatch();
        private readonly Timer clockTimer = new Timer();
        private readonly ToolTip hints = new ToolTip();
        private readonly Font titleFont = new Font("Arial", 21.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font timerFont = new Font("Arial", 13.5f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font displayFont = new Font("Arial", 35f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font expressionFont = new Font("Arial", 16f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font buttonFont = new Font("Arial", 18f, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font memoryFont = new Font("Arial", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font smallSymbolFont = new Font("Arial", 24f, FontStyle.Regular, GraphicsUnit.Pixel);

        private string entry = "0";
        private string expression = "";
        private string pendingOperator = null;
        private decimal accumulator = 0m;
        private decimal memory = 0m;
        private bool replaceEntry = true;
        private bool error = false;
        private bool timerPaused = false;
        private string repeatOperator = null;
        private decimal repeatOperand = 0m;

        private readonly Color blue = Color.FromArgb(65, 116, 184);
        private readonly Color body = Color.FromArgb(239, 239, 239);
        private readonly Color red = Color.FromArgb(211, 69, 57);
        private readonly Color green = Color.FromArgb(85, 195, 101);
        private readonly Rectangle closeRect = new Rectangle(304, 15, 29, 28);
        private readonly Rectangle timerRect = new Rectangle(200, 12, 94, 37);
        private readonly Rectangle[] rowButtons = new Rectangle[30];
        private readonly string[] rowLabels = new string[30];
        private readonly string[] rowActions = new string[30];
        private int buttonCount;
        private Rectangle equalsRect;
        private Rectangle zeroRect;
        private Rectangle backspaceRect;

        public CalculatorForm()
        {
            Text = "Calculator";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(WindowWidth, WindowHeight);
            MinimumSize = MaximumSize = Size;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            TopMost = true;
            BackColor = Color.FromArgb(234, 234, 234);
            DoubleBuffered = true;
            ShowInTaskbar = true;

            GraphicsPath shape = RoundedRectangle(new Rectangle(0, 0, WindowWidth, WindowHeight), 11);
            Region = new Region(shape);
            shape.Dispose();

            BuildButtons();
            hints.SetToolTip(this, "Calculator — use the stopwatch to pause or resume; right-click it to reset.");
            clock.Start();
            clockTimer.Interval = 1000;
            clockTimer.Tick += delegate { Invalidate(new Rectangle(190, 5, 112, 51)); };
            clockTimer.Start();
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseLeave += delegate { if (pressedButton >= 0) { pressedButton = -1; Invalidate(); } };
            KeyDown += OnKeyDown;
            KeyPress += OnKeyPress;
        }

        private void BuildButtons()
        {
            Add("MC", "memory-clear", Col(0), 168, ColWidth, ButtonHeight);
            Add("MR", "memory-recall", Col(1), 168, ColWidth, ButtonHeight);
            Add("MS", "memory-store", Col(2), 168, ColWidth, ButtonHeight);
            Add("M+", "memory-add", Col(3), 168, ColWidth, ButtonHeight);
            Add("M-", "memory-subtract", Col(4), 168, ColWidth, ButtonHeight);

            backspaceRect = new Rectangle(GridX, 211, ColWidth * 2 + Gap, ButtonHeight);
            Add("←", "backspace", backspaceRect);
            Add("C", "clear", Col(2), 211, ColWidth, ButtonHeight);
            Add("+/−", "sign", Col(3), 211, ColWidth, ButtonHeight);
            Add("√", "sqrt", Col(4), 211, ColWidth, ButtonHeight);

            Add("7", "digit:7", Col(0), 253, ColWidth, ButtonHeight);
            Add("8", "digit:8", Col(1), 253, ColWidth, ButtonHeight);
            Add("9", "digit:9", Col(2), 253, ColWidth, ButtonHeight);
            Add("/", "operator:/", Col(3), 253, ColWidth, ButtonHeight);
            Add("%", "percent", Col(4), 253, ColWidth, ButtonHeight);

            Add("4", "digit:4", Col(0), 294, ColWidth, ButtonHeight);
            Add("5", "digit:5", Col(1), 294, ColWidth, ButtonHeight);
            Add("6", "digit:6", Col(2), 294, ColWidth, ButtonHeight);
            Add("*", "operator:*", Col(3), 294, ColWidth, ButtonHeight);
            Add("1/x", "reciprocal", Col(4), 294, ColWidth, ButtonHeight);

            Add("1", "digit:1", Col(0), 335, ColWidth, ButtonHeight);
            Add("2", "digit:2", Col(1), 335, ColWidth, ButtonHeight);
            Add("3", "digit:3", Col(2), 335, ColWidth, ButtonHeight);
            Add("-", "operator:-", Col(3), 335, ColWidth, ButtonHeight);
            equalsRect = new Rectangle(Col(4), 335, ColWidth, 74);
            Add("=", "equals", equalsRect);

            zeroRect = new Rectangle(GridX, 376, ColWidth * 2 + Gap, ButtonHeight);
            Add("0", "digit:0", zeroRect);
            Add(".", "decimal", Col(2), 376, ColWidth, ButtonHeight);
            Add("+", "operator:+", Col(3), 376, ColWidth, ButtonHeight);
        }

        private int Col(int index) { return GridX + index * (ColWidth + Gap); }

        private void Add(string label, string action, int x, int y, int w, int h)
        {
            Add(label, action, new Rectangle(x, y, w, h));
        }

        private void Add(string label, string action, Rectangle rect)
        {
            rowButtons[buttonCount] = rect;
            rowLabels[buttonCount] = label;
            rowActions[buttonCount] = action;
            buttonCount++;
        }

        private static GraphicsPath RoundedRectangle(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush outer = new SolidBrush(Color.FromArgb(248, 248, 248)))
                g.FillRectangle(outer, ClientRectangle);
            using (Pen border = new Pen(Color.FromArgb(205, 205, 205), 1f))
            {
                Rectangle outer = new Rectangle(1, 1, WindowWidth - 3, WindowHeight - 3);
                using (GraphicsPath outline = RoundedRectangle(outer, 10)) g.DrawPath(border, outline);
            }

            using (GraphicsPath header = RoundedRectangle(new Rectangle(6, 7, 330, 49), 10))
            using (SolidBrush titleBlue = new SolidBrush(blue))
                g.FillPath(titleBlue, header);

            using (GraphicsPath panel = RoundedRectangle(new Rectangle(7, 59, 328, 355), 10))
            using (SolidBrush panelBrush = new SolidBrush(body))
                g.FillPath(panelBrush, panel);

            DrawText(g, "Calculator", titleFont, Color.White, new Rectangle(28, 13, 150, 36), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            DrawStopwatch(g);
            DrawClose(g);

            DrawDisplay(g, new Rectangle(23, 67, 286, 42), expression, false);
            DrawDisplay(g, new Rectangle(23, 117, 286, 43), error ? "Error" : entry, true);

            for (int i = 0; i < buttonCount; i++)
                DrawButton(g, i);
        }

        private static void DrawText(Graphics g, string text, Font font, Color color, Rectangle rect, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text, font, rect, color, flags);
        }

        private void DrawStopwatch(Graphics g)
        {
            int cx = 210, cy = 30;
            using (Pen p = new Pen(Color.White, 1.6f))
            {
                g.DrawEllipse(p, cx - 7, cy - 8, 14, 14);
                g.DrawLine(p, cx, cy - 8, cx, cy - 11);
                g.DrawLine(p, cx - 3, cy - 12, cx + 3, cy - 12);
                g.DrawLine(p, cx + 7, cy - 7, cx + 9, cy - 9);
                g.DrawLine(p, cx, cy - 4, cx, cy + 1);
            }
            TimeSpan elapsed = clock.Elapsed;
            int hours = (int)Math.Min(99, elapsed.TotalHours);
            string stamp = hours.ToString("00", CultureInfo.InvariantCulture) + ":" + elapsed.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + elapsed.Seconds.ToString("00", CultureInfo.InvariantCulture);
            DrawText(g, stamp, timerFont, Color.White, new Rectangle(223, 14, 77, 31), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        private void DrawClose(Graphics g)
        {
            using (GraphicsPath tile = RoundedRectangle(closeRect, 6))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(249, 249, 249)))
            using (Pen outline = new Pen(Color.FromArgb(226, 226, 226), 1f))
            {
                g.FillPath(fill, tile);
                g.DrawPath(outline, tile);
            }
            using (Pen x = new Pen(Color.FromArgb(230, 139, 35), 3.8f))
            {
                x.StartCap = LineCap.Round;
                x.EndCap = LineCap.Round;
                g.DrawLine(x, 313, 22, 325, 36);
                g.DrawLine(x, 325, 22, 313, 36);
            }
        }

        private void DrawDisplay(Graphics g, Rectangle rect, string value, bool result)
        {
            Rectangle shadow = new Rectangle(rect.X, rect.Y + 4, rect.Width, rect.Height - 1);
            using (GraphicsPath shadowPath = RoundedRectangle(shadow, 4))
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(166, 166, 166)))
                g.FillPath(shadowBrush, shadowPath);

            using (GraphicsPath face = RoundedRectangle(rect, 4))
            using (SolidBrush white = new SolidBrush(Color.White))
            using (Pen edge = new Pen(Color.FromArgb(183, 183, 183), 1.1f))
            {
                g.FillPath(white, face);
                g.DrawPath(edge, face);
            }

            if (!String.IsNullOrEmpty(value))
            {
                Font fit = result ? displayFont : expressionFont;
                float size = result ? 35f : 16f;
                Size measured = TextRenderer.MeasureText(value, fit, Size.Empty, TextFormatFlags.NoPadding);
                while (measured.Width > rect.Width - 16 && size > 15f)
                {
                    size -= 1f;
                    fit = new Font("Arial", size, FontStyle.Regular, GraphicsUnit.Pixel);
                    measured = TextRenderer.MeasureText(value, fit, Size.Empty, TextFormatFlags.NoPadding);
                }
                DrawText(g, value, fit, result ? Color.Black : Color.FromArgb(75, 75, 75), new Rectangle(rect.X + 7, rect.Y + 1, rect.Width - 17, rect.Height - 3), TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                if (!Object.ReferenceEquals(fit, result ? displayFont : expressionFont)) fit.Dispose();
            }
        }

        private int pressedButton = -1;

        private void DrawButton(Graphics g, int index)
        {
            Rectangle rect = rowButtons[index];
            string action = rowActions[index];
            bool isRed = action == "backspace" || action == "clear" || action == "sign";
            bool isGreen = action == "equals";
            Color fillColor = isRed ? red : (isGreen ? green : Color.FromArgb(247, 247, 247));
            Color outlineColor = isRed ? Color.FromArgb(176, 59, 48) : (isGreen ? Color.FromArgb(64, 156, 78) : Color.FromArgb(185, 185, 185));
            Color shadowColor = isRed ? Color.FromArgb(171, 61, 51) : (isGreen ? Color.FromArgb(58, 150, 73) : Color.FromArgb(164, 164, 164));
            int shadowDepth = isGreen ? 4 : 4;
            Rectangle shadow = new Rectangle(rect.X, rect.Y + shadowDepth, rect.Width, rect.Height - 1);
            using (GraphicsPath shadowPath = RoundedRectangle(shadow, 6))
            using (SolidBrush shadowBrush = new SolidBrush(shadowColor))
                g.FillPath(shadowBrush, shadowPath);

            Rectangle faceRect = pressedButton == index ? new Rectangle(rect.X, rect.Y + 2, rect.Width, rect.Height - 2) : rect;
            using (GraphicsPath face = RoundedRectangle(faceRect, 6))
            using (SolidBrush fill = new SolidBrush(fillColor))
            using (Pen outline = new Pen(outlineColor, 1.15f))
            {
                g.FillPath(fill, face);
                g.DrawPath(outline, face);
            }

            if (action == "backspace")
            {
                using (Pen arrow = new Pen(Color.White, 4.0f))
                {
                    arrow.StartCap = LineCap.Round; arrow.EndCap = LineCap.Round;
                    g.DrawLine(arrow, rect.X + 37, rect.Y + 17, rect.X + 67, rect.Y + 17);
                    g.DrawLine(arrow, rect.X + 37, rect.Y + 17, rect.X + 46, rect.Y + 8);
                    g.DrawLine(arrow, rect.X + 37, rect.Y + 17, rect.X + 46, rect.Y + 26);
                }
                return;
            }

            Font font = (action.StartsWith("memory-", StringComparison.Ordinal)) ? memoryFont : buttonFont;
            if (action == "sqrt") font = smallSymbolFont;
            if (action == "sign") font = new Font("Arial", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
            Color textColor = (isRed || isGreen) ? Color.White : Color.FromArgb(35, 35, 35);
            DrawText(g, rowLabels[index], font, textColor, faceRect, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            if (action == "sign") font.Dispose();
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (closeRect.Contains(e.Location))
            {
                if (e.Button == MouseButtons.Left) Close();
                return;
            }
            if (timerRect.Contains(e.Location))
            {
                if (e.Button == MouseButtons.Right)
                {
                    clock.Reset();
                    if (!timerPaused) clock.Start();
                }
                else if (e.Button == MouseButtons.Left)
                {
                    timerPaused = !timerPaused;
                    if (timerPaused) clock.Stop(); else clock.Start();
                }
                Invalidate(new Rectangle(190, 5, 112, 51));
                return;
            }
            for (int i = 0; i < buttonCount; i++)
            {
                if (rowButtons[i].Contains(e.Location))
                {
                    pressedButton = i;
                    Invalidate(rowButtons[i]);
                    if (e.Button == MouseButtons.Left) DoAction(rowActions[i]);
                    return;
                }
            }
            if (e.Button == MouseButtons.Left && e.Y >= 6 && e.Y < 57)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            // Buttons only depress while the mouse button is held; the reference has no hover treatment.
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            int old = pressedButton; pressedButton = -1;
            if (old >= 0) Invalidate(rowButtons[old]);
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar >= '0' && e.KeyChar <= '9') { DoAction("digit:" + e.KeyChar); e.Handled = true; }
            else if (e.KeyChar == '.' || e.KeyChar == ',') { DoAction("decimal"); e.Handled = true; }
            else if (e.KeyChar == '+' || e.KeyChar == '-' || e.KeyChar == '*' || e.KeyChar == '/') { DoAction("operator:" + e.KeyChar); e.Handled = true; }
            else if (e.KeyChar == '%') { DoAction("percent"); e.Handled = true; }
            else if (e.KeyChar == '=') { DoAction("equals"); e.Handled = true; }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Back) { DoAction("backspace"); e.Handled = e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Delete) { DoAction("clear"); e.Handled = e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter) { DoAction("equals"); e.Handled = e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F9) { DoAction("sign"); e.Handled = e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.R) { DoAction("sqrt"); e.Handled = e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.I) { DoAction("reciprocal"); e.Handled = e.SuppressKeyPress = true; }
        }

        private bool TryValue(out decimal value)
        {
            return Decimal.TryParse(entry, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private decimal Current
        {
            get { decimal value; return TryValue(out value) ? value : 0m; }
        }

        private static string Format(decimal value)
        {
            return value.ToString("G29", CultureInfo.InvariantCulture);
        }

        private void DoAction(string action)
        {
            if (error && !action.StartsWith("digit:", StringComparison.Ordinal) && action != "clear")
                return;
            if (action == "clear") { ClearAll(); return; }
            if (action == "backspace")
            {
                if (replaceEntry) { entry = "0"; replaceEntry = false; }
                else if (entry.Length > 1) entry = entry.Substring(0, entry.Length - 1);
                else if (entry.Length == 1 || entry.Length == 2 && entry[0] == '-') entry = "0";
                else if (entry.Length > 1 && entry[0] == '-') entry = "-" + entry.Substring(1, entry.Length - 2);
                RefreshDisplay(); return;
            }
            if (action.StartsWith("digit:", StringComparison.Ordinal))
            {
                string digit = action.Substring(6);
                if (error) ClearAll();
                if (replaceEntry || entry == "0") { entry = digit; replaceEntry = false; }
                else if (entry.Length < 28) entry += digit;
                expression = pendingOperator == null ? "" : expression;
                RefreshDisplay(); return;
            }
            if (action == "decimal")
            {
                if (replaceEntry) { entry = "0."; replaceEntry = false; }
                else if (entry.IndexOf('.') < 0) entry += ".";
                RefreshDisplay(); return;
            }
            if (action.StartsWith("operator:", StringComparison.Ordinal)) { SetOperator(action.Substring(9)); return; }
            if (action == "equals") { EqualsAction(); return; }
            if (action == "sign")
            {
                decimal value = Current;
                if (value != 0m) entry = Format(-value);
                RefreshDisplay(); return;
            }
            if (action == "percent")
            {
                decimal value = Current;
                if (pendingOperator == "+" || pendingOperator == "-") value = accumulator * value / 100m;
                else value /= 100m;
                entry = Format(value); replaceEntry = true;
                expression = "Percent";
                RefreshDisplay(); return;
            }
            if (action == "sqrt")
            {
                decimal value = Current;
                if (value < 0m) { SetError(); return; }
                entry = Format((decimal)Math.Sqrt((double)value)); replaceEntry = true; expression = "√(" + Format(value) + ")";
                RefreshDisplay(); return;
            }
            if (action == "reciprocal")
            {
                decimal value = Current;
                if (value == 0m) { SetError(); return; }
                entry = Format(1m / value); replaceEntry = true; expression = "1/" + Format(value);
                RefreshDisplay(); return;
            }
            if (action == "memory-clear") memory = 0m;
            else if (action == "memory-recall") { entry = Format(memory); replaceEntry = true; expression = ""; error = false; }
            else if (action == "memory-store") memory = Current;
            else if (action == "memory-add") memory += Current;
            else if (action == "memory-subtract") memory -= Current;
            RefreshDisplay();
        }

        private void ClearAll()
        {
            entry = "0"; expression = ""; pendingOperator = null; accumulator = 0m;
            replaceEntry = true; error = false; repeatOperator = null; repeatOperand = 0m;
            RefreshDisplay();
        }

        private void SetOperator(string op)
        {
            decimal value = Current;
            if (pendingOperator != null && !replaceEntry)
            {
                if (!Apply(pendingOperator, accumulator, value, out accumulator)) { SetError(); return; }
                entry = Format(accumulator);
            }
            else accumulator = value;
            pendingOperator = op;
            expression = Format(accumulator) + " " + OperatorGlyph(op);
            replaceEntry = true;
            repeatOperator = null;
            RefreshDisplay();
        }

        private void EqualsAction()
        {
            if (pendingOperator != null)
            {
                decimal right = Current, result;
                if (!Apply(pendingOperator, accumulator, right, out result)) { SetError(); return; }
                expression = Format(accumulator) + " " + OperatorGlyph(pendingOperator) + " " + Format(right) + " =";
                repeatOperator = pendingOperator; repeatOperand = right;
                pendingOperator = null; accumulator = result; entry = Format(result); replaceEntry = true;
            }
            else if (replaceEntry && repeatOperator != null)
            {
                decimal result;
                if (!Apply(repeatOperator, Current, repeatOperand, out result)) { SetError(); return; }
                expression = Format(Current) + " " + OperatorGlyph(repeatOperator) + " " + Format(repeatOperand) + " =";
                entry = Format(result); accumulator = result;
            }
            RefreshDisplay();
        }

        private static bool Apply(string op, decimal left, decimal right, out decimal result)
        {
            result = 0m;
            try
            {
                switch (op)
                {
                    case "+": result = left + right; return true;
                    case "-": result = left - right; return true;
                    case "*": result = left * right; return true;
                    case "/": if (right == 0m) return false; result = left / right; return true;
                    default: return false;
                }
            }
            catch (OverflowException) { return false; }
        }

        private static string OperatorGlyph(string op) { return op == "*" ? "×" : (op == "/" ? "÷" : op); }

        private void SetError()
        {
            error = true; entry = "0"; expression = "Cannot divide by zero"; pendingOperator = null; replaceEntry = true; RefreshDisplay();
        }

        private void RefreshDisplay() { Invalidate(new Rectangle(14, 61, 310, 104)); }
    }
}
