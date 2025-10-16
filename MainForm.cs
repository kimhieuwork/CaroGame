// Caro (Gomoku) - WinForms .NET Framework 4.8
// Single-file example: Program + MainForm + SettingsForm
// Features implemented (as requested):
// - Default board: 100x100 on start
// - X goes first then O
// - Theme selection (BlackWhite, Green, Orange, Blue)
// - Toggle piece coloring (on/off). When off pieces become monochrome (Black or White) selectable in settings.
// - Dialog displayed when a player wins
// - Undo button (reverts last move)
// - New Game button
// - Settings dialog: choose board size among 3x3, 5x5, 10x10, 100x100; rules: for 3/5/10 -> 3 in row to win; for 100 -> 5 in row to win
// - Efficient rendering with double buffering and AutoScroll panel (supports large boards)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CaroGame
{
    public enum CellState { Empty = 0, X = 1, O = 2 }
    public enum Theme { BlackWhite, Green, Orange, Blue }

    public partial class MainForm : Form
    {
        // Game data
        private int rows = 100;
        private int cols = 100;
        private int winLength = 5; // default for 100x100
        private CellState[,] board;
        private Stack<(int r, int c)> moves = new Stack<(int, int)>();
        private CellState currentPlayer = CellState.X;

        // UI
        private Panel boardPanel;
        private ToolStrip toolStrip;
        private ToolStripButton btnNewGame, btnUndo, btnSettings, btnToggleColor;
        private ToolStripComboBox cmbTheme;
        private Label lblStatus;

        // Rendering
        private int cellSize = 20; // will be adjusted
        private Theme currentTheme = Theme.BlackWhite;
        private bool pieceColorEnabled = true; // if false use monochromeChoice
        private Color monoColor = Color.Black; // when pieceColorEnabled==false, this is the color used for both pieces (user chooses Black or White in settings)

        // Colors by theme
        private Color boardBack = Color.White;
        private Color gridColor = Color.Black;
        private Color xColor = Color.Red;
        private Color oColor = Color.Blue;

        public MainForm()
        {
            Text = "Caro (Gomoku) - WinForms (.NET 4.8)";
            WindowState = FormWindowState.Maximized;
            InitializeComponents();
            StartNewGame(rows, cols, winLength);
        }

        private void InitializeComponents()
        {
            // Tool strip
            toolStrip = new ToolStrip();
            btnNewGame = new ToolStripButton("New Game");
            btnNewGame.Click += (s, e) => { StartNewGame(rows, cols, winLength); };
            btnUndo = new ToolStripButton("Undo");
            btnUndo.Click += (s, e) => { UndoMove(); };
            btnSettings = new ToolStripButton("Settings");
            btnSettings.Click += (s, e) => { OpenSettings(); };
            btnToggleColor = new ToolStripButton("Toggle Piece Color");
            btnToggleColor.Click += (s, e) => { pieceColorEnabled = !pieceColorEnabled; ApplyTheme(); boardPanel.Invalidate(); UpdateStatus(); };

            cmbTheme = new ToolStripComboBox();
            cmbTheme.Items.AddRange(new string[] { "BlackWhite", "Green", "Orange", "Blue" });
            cmbTheme.SelectedIndex = 0;
            cmbTheme.SelectedIndexChanged += (s, e) => {
                currentTheme = (Theme)cmbTheme.SelectedIndex;
                ApplyTheme();
                boardPanel.Invalidate();
            };

            toolStrip.Items.Add(btnNewGame);
            toolStrip.Items.Add(btnUndo);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(new ToolStripLabel("Theme:"));
            toolStrip.Items.Add(cmbTheme);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnToggleColor);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnSettings);

            Controls.Add(toolStrip);

            // Status label
            lblStatus = new Label();
            lblStatus.Dock = DockStyle.Bottom;
            lblStatus.Height = 24;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lblStatus);

            // Board panel
            boardPanel = new Panel();
            boardPanel.Dock = DockStyle.Fill;
            boardPanel.AutoScroll = true;
            boardPanel.BackColor = Color.White;
            boardPanel.Paint += BoardPanel_Paint;
            boardPanel.MouseClick += BoardPanel_MouseClick;
            boardPanel.Resize += (s, e) => boardPanel.Invalidate();

            // enable double buffering for smoother drawing
            typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(boardPanel, true, null);

            Controls.Add(boardPanel);
        }

        private void StartNewGame(int r, int c, int winLen)
        {
            rows = r; cols = c; winLength = winLen;
            board = new CellState[rows, cols];
            moves.Clear();
            currentPlayer = CellState.X;
            AdjustCellSize();
            ApplyTheme();
            boardPanel.Invalidate();
            UpdateStatus();
        }

        private void AdjustCellSize()
        {
            // choose cellSize so board is reasonably sized but allow scroll for very large boards
            int targetPixels = 1000; // try to have board visible area ~1000 px
            cellSize = Math.Max(8, Math.Min(40, targetPixels / Math.Max(rows, cols)));
            // For small boards, enlarge cells
            if (rows <= 10) cellSize = Math.Max(cellSize, 40);

            // set virtual size of panel
            boardPanel.AutoScrollMinSize = new Size(cols * cellSize, rows * cellSize);
        }

        private void ApplyTheme()
        {
            switch (currentTheme)
            {
                case Theme.BlackWhite:
                    boardBack = Color.White;
                    gridColor = Color.Black;
                    xColor = Color.Black;
                    oColor = Color.DimGray;
                    break;
                case Theme.Green:
                    boardBack = Color.FromArgb(240, 255, 240);
                    gridColor = Color.FromArgb(30, 80, 30);
                    xColor = Color.FromArgb(10, 120, 10);
                    oColor = Color.FromArgb(0, 80, 255);
                    break;
                case Theme.Orange:
                    boardBack = Color.FromArgb(255, 245, 230);
                    gridColor = Color.FromArgb(120, 70, 10);
                    xColor = Color.FromArgb(220, 80, 10);
                    oColor = Color.FromArgb(0, 90, 180);
                    break;
                case Theme.Blue:
                    boardBack = Color.FromArgb(240, 248, 255);
                    gridColor = Color.FromArgb(20, 40, 90);
                    xColor = Color.FromArgb(190, 20, 90);
                    oColor = Color.FromArgb(10, 70, 200);
                    break;
            }

            if (!pieceColorEnabled)
            {
                xColor = monoColor;
                oColor = monoColor;
            }

            boardPanel.BackColor = boardBack;
        }

        private void BoardPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TranslateTransform(boardPanel.AutoScrollPosition.X, boardPanel.AutoScrollPosition.Y);

            // draw grid background
            g.Clear(boardBack);

            Pen penGrid = new Pen(gridColor);

            // Draw vertical and horizontal lines
            for (int c = 0; c <= cols; c++)
            {
                int x = c * cellSize;
                g.DrawLine(penGrid, x, 0, x, rows * cellSize);
            }
            for (int r = 0; r <= rows; r++)
            {
                int y = r * cellSize;
                g.DrawLine(penGrid, 0, y, cols * cellSize, y);
            }

            // draw pieces - center them within cell
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (board[r, c] == CellState.Empty) continue;
                    Rectangle cellRect = new Rectangle(c * cellSize, r * cellSize, cellSize, cellSize);
                    DrawPiece(g, board[r, c], cellRect);
                }
            }

            penGrid.Dispose();
        }

        private void DrawPiece(Graphics g, CellState who, Rectangle rect)
        {
            int padding = Math.Max(2, rect.Width / 10);
            Rectangle inner = new Rectangle(rect.X + padding, rect.Y + padding, rect.Width - padding * 2, rect.Height - padding * 2);

            Color col = (who == CellState.X) ? xColor : oColor;

            if (who == CellState.X)
            {
                // draw X
                using (Pen p = new Pen(col, Math.Max(1, rect.Width / 10)))
                {
                    g.DrawLine(p, inner.Left, inner.Top, inner.Right, inner.Bottom);
                    g.DrawLine(p, inner.Left, inner.Bottom, inner.Right, inner.Top);
                }
            }
            else
            {
                // draw O
                using (Pen p = new Pen(col, Math.Max(1, rect.Width / 10)))
                {
                    g.DrawEllipse(p, inner);
                }
            }
        }

        private void BoardPanel_MouseClick(object sender, MouseEventArgs e)
        {
            // map mouse to cell considering scroll
            int offsetX = -boardPanel.AutoScrollPosition.X;
            int offsetY = -boardPanel.AutoScrollPosition.Y;
            int x = e.X + offsetX;
            int y = e.Y + offsetY;
            int c = x / cellSize;
            int r = y / cellSize;
            if (r < 0 || r >= rows || c < 0 || c >= cols) return;

            if (board[r, c] != CellState.Empty)
            {
                // already filled
                return;
            }

            board[r, c] = currentPlayer;
            moves.Push((r, c));
            boardPanel.Invalidate(new Rectangle(c * cellSize, r * cellSize, cellSize, cellSize));

            if (CheckWin(r, c))
            {
                string winner = (currentPlayer == CellState.X) ? "X" : "O";
                MessageBox.Show($"Người chơi {winner} đã thắng!", "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
                // optionally freeze board until new game or allow play to continue; we'll stop further moves
                // To allow continuing we can simply return without switching player
                // Here, disable clicking by setting currentPlayer to Empty
                currentPlayer = CellState.Empty;
                UpdateStatus();
                return;
            }

            // switch player
            currentPlayer = (currentPlayer == CellState.X) ? CellState.O : CellState.X;
            UpdateStatus();
        }

        private bool CheckWin(int lastR, int lastC)
        {
            CellState player = board[lastR, lastC];
            if (player == CellState.Empty) return false;

            // check 4 directions
            var dirs = new (int dr, int dc)[] { (0, 1), (1, 0), (1, 1), (1, -1) };
            foreach (var d in dirs)
            {
                int count = 1;
                // forward
                int r = lastR + d.dr, c = lastC + d.dc;
                while (IsOnBoard(r, c) && board[r, c] == player)
                {
                    count++; r += d.dr; c += d.dc;
                }
                // backward
                r = lastR - d.dr; c = lastC - d.dc;
                while (IsOnBoard(r, c) && board[r, c] == player)
                {
                    count++; r -= d.dr; c -= d.dc;
                }
                if (count >= winLength) return true;
            }
            return false;
        }

        private bool IsOnBoard(int r, int c) => r >= 0 && r < rows && c >= 0 && c < cols;

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(972, 531);
            this.Name = "MainForm";
            this.ResumeLayout(false);

        }

        private void UndoMove()
        {
            if (moves.Count == 0 || currentPlayer == CellState.Empty) return;
            var last = moves.Pop();
            board[last.r, last.c] = CellState.Empty;
            // switch back player
            currentPlayer = (currentPlayer == CellState.X) ? CellState.O : CellState.X;
            boardPanel.Invalidate(new Rectangle(last.c * cellSize, last.r * cellSize, cellSize, cellSize));
            UpdateStatus();
        }

        private void OpenSettings()
        {
            using (var dlg = new SettingsForm(rows, cols, winLength, pieceColorEnabled, monoColor, currentTheme))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // apply settings
                    rows = dlg.SelectedRows;
                    cols = dlg.SelectedCols;
                    winLength = dlg.SelectedWinLength;
                    pieceColorEnabled = dlg.PieceColorEnabled;
                    monoColor = dlg.MonoColor;
                    currentTheme = dlg.SelectedTheme;
                    cmbTheme.SelectedIndex = (int)currentTheme;
                    StartNewGame(rows, cols, winLength);
                }
            }
        }

        private void UpdateStatus()
        {
            if (currentPlayer == CellState.Empty)
            {
                lblStatus.Text = "Trò chơi kết thúc. Nhấn New Game để bắt đầu lại.";
            }
            else
            {
                string p = (currentPlayer == CellState.X) ? "X" : "O";
                lblStatus.Text = $"Lượt: {p}   Kích thước: {rows}x{cols}   Quy tắc thắng: {winLength} nối liên tiếp";
            }
        }
    }

    public class SettingsForm : Form
    {
        public int SelectedRows { get; private set; }
        public int SelectedCols { get; private set; }
        public int SelectedWinLength { get; private set; }
        public bool PieceColorEnabled { get; private set; }
        public Color MonoColor { get; private set; }
        public Theme SelectedTheme { get; private set; }

        private ComboBox cmbSize;
        private RadioButton rbMonoBlack, rbMonoWhite, rbColorOn, rbColorOff;
        private ComboBox cmbTheme;

        public SettingsForm(int currentRows, int currentCols, int currentWin, bool pieceColorEnabled, Color mono, Theme currentTheme)
        {
            Text = "Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            Width = 360;
            Height = 260;
            SelectedRows = currentRows;
            SelectedCols = currentCols;
            SelectedWinLength = currentWin;
            PieceColorEnabled = pieceColorEnabled;
            MonoColor = mono;
            SelectedTheme = currentTheme;

            InitializeComponents();
            LoadValues();
        }

        private void InitializeComponents()
        {
            var lblSize = new Label() { Text = "Board size:", Left = 12, Top = 16, Width = 80 };
            cmbSize = new ComboBox() { Left = 100, Top = 12, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbSize.Items.AddRange(new string[] { "3 x 3", "5 x 5", "10 x 10", "100 x 100" });
            Controls.Add(lblSize);
            Controls.Add(cmbSize);

            var lblWin = new Label() { Text = "Win rule:", Left = 12, Top = 56, Width = 80 };
            var lblWinInfo = new Label() { Left = 100, Top = 56, Width = 220, Height = 28, Text = "Auto: 3 for small sizes, 5 for 100x100" };
            Controls.Add(lblWin);
            Controls.Add(lblWinInfo);

            var lblTheme = new Label() { Text = "Theme:", Left = 12, Top = 96, Width = 80 };
            cmbTheme = new ComboBox() { Left = 100, Top = 92, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTheme.Items.AddRange(new string[] { "BlackWhite", "Green", "Orange", "Blue" });
            Controls.Add(lblTheme);
            Controls.Add(cmbTheme);

            var lblPieceColor = new Label() { Text = "Piece color:", Left = 12, Top = 136, Width = 80 };
            rbColorOn = new RadioButton() { Text = "Color ON", Left = 100, Top = 132, Width = 100 };
            rbColorOff = new RadioButton() { Text = "Color OFF (Mono):", Left = 200, Top = 132, Width = 140 };
            Controls.Add(lblPieceColor);
            Controls.Add(rbColorOn);
            Controls.Add(rbColorOff);

            rbMonoBlack = new RadioButton() { Text = "Black", Left = 120, Top = 162, Width = 80 };
            rbMonoWhite = new RadioButton() { Text = "White", Left = 200, Top = 162, Width = 80 };
            Controls.Add(rbMonoBlack);
            Controls.Add(rbMonoWhite);

            var btnOk = new Button() { Text = "OK", Left = 140, Top = 200, Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button() { Text = "Cancel", Left = 230, Top = 200, Width = 80, DialogResult = DialogResult.Cancel };
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            btnOk.Click += BtnOk_Click;
        }

        private void LoadValues()
        {
            if (SelectedRows == 3) cmbSize.SelectedIndex = 0;
            else if (SelectedRows == 5) cmbSize.SelectedIndex = 1;
            else if (SelectedRows == 10) cmbSize.SelectedIndex = 2;
            else cmbSize.SelectedIndex = 3; // default 100x100

            cmbTheme.SelectedIndex = (int)SelectedTheme;

            if (PieceColorEnabled) rbColorOn.Checked = true; else rbColorOff.Checked = true;
            if (MonoColor == Color.Black) rbMonoBlack.Checked = true; else rbMonoWhite.Checked = true;

            // enable / disable mono controls based on radio
            rbColorOn.CheckedChanged += (s, e) => { rbMonoBlack.Enabled = rbMonoWhite.Enabled = !rbColorOn.Checked; };
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            // parse size
            switch (cmbSize.SelectedIndex)
            {
                case 0: SelectedRows = SelectedCols = 3; SelectedWinLength = 3; break;
                case 1: SelectedRows = SelectedCols = 5; SelectedWinLength = 3; break;
                case 2: SelectedRows = SelectedCols = 10; SelectedWinLength = 3; break;
                default: SelectedRows = SelectedCols = 100; SelectedWinLength = 5; break;
            }

            PieceColorEnabled = rbColorOn.Checked;
            MonoColor = rbMonoBlack.Checked ? Color.Black : Color.White;
            SelectedTheme = (Theme)cmbTheme.SelectedIndex;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
