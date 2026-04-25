using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SharpDX.DirectInput;

namespace Turbo.Plugins.Default
{
    /// <summary>
    /// DPS-meter style overlay for Greater Rifts.
    /// Displays the history of the last N runs with:
    ///   - Ascending timer 0:00 → 15:00 (elapsed time, not remaining time)
    ///   - GR level
    ///   - Result (killed in time or timeout)
    /// Window is repositionable by dragging the title bar.
    /// Toggle: T key
    /// </summary>
    public class GRiftTimerPlugin : BasePlugin, IInGameTopPainter, IAfterCollectHandler, IKeyEventHandler
    {
        // ── Toggle + Reset + Config ────────────────────────────────────────
        public IKeyEvent ToggleKeyEvent { get; set; }
        public IKeyEvent ResetKeyEvent  { get; set; }
        public IKeyEvent ConfigKeyEvent { get; set; }
        public bool Visible { get; set; } = true;

        // ── Layout ────────────────────────────────────────────────────────────
        private const float WIN_W    = 460f;
        private const float HDR_H    = 22f;
        private const float COL_H    = 17f;
        private const float ROW_H    = 22f;
        private const float PAD      = 8f;

        private const float COL_NUM  = PAD;    // "#"      left at 8px
        private const float COL_GR   = 46f;   // "GR"     left at 46px  (fits "#99" before it)
        private const float COL_TIME = 115f;  // "Time"   left at 115px (fits "GR150")
        private const float COL_RES  = 255f;  // "Result" left at 255px

        private const float BAR_ROW_H = 22f;  // height of the combined timer bar row

        // ── Hover buttons ─────────────────────────────────────────────────────
        private const float BTN_W    = 26f;
        private const float BTN_H    = 16f;
        private const float BTN_GAP  = 4f;
        private const long  HOVER_MS = 1500;   // ms to hold cursor to trigger

        // ── (GR level via IPlayer.InGreaterRiftRank) ──────────────────────────

        // ── State ─────────────────────────────────────────────────────────────
        private bool   _wasInGR        = false;
        private int    _startTick      = 0;
        private int    _currentGRLevel = 0;
        private double _lastElapsed    = 0.0;
        private float  _lastPercent    = 0f;
        private int    _runNumber      = 0;

        // ── Hover button state ────────────────────────────────────────────────
        private int  _hoverBtn      = 0;   // 0=none, 1=[T], 2=[R], 3=[S]
        private long _hoverStartMs  = 0;
        private int  _hoverConsumed = 0;   // button locked until cursor leaves

        // Floor change: wait X ms before validating a rift end
        private bool   _pendingSave              = false;
        private long   _leaveTimeMs              = 0;
        private int    _lastCommittedStartTick   = 0;
        private long   _floorGraceMs             = 60000;

        // ── Localization ──────────────────────────────────────────────────────
        // Supported values: en, fr, de, es
        private string _lang = "en";

        // ── Timer bar color thresholds ─────────────────────────────────────────
        private double _threshOrangeSec = 90.0;   // green below, orange above (default 1:30)
        private double _threshRedSec    = 100.0;  // orange below, red above (default 1:40)

        // ── History ───────────────────────────────────────────────────────────
        private int    _maxRuns     = 10;
        private string _resetAction = "archive"; // "archive" or "delete"
        private readonly List<RiftRun> _history = new List<RiftRun>();

        private class RiftRun
        {
            public int    Number;
            public int    GRLevel;
            public double ElapsedSeconds;
            public bool   Completed;
        }

        // ── Window position ───────────────────────────────────────────────────
        private float _winX = -1f;
        private float _winY = -1f;
        private bool  _posInitialized = false;

        // ── Drag ──────────────────────────────────────────────────────────────
        private bool  _dragging    = false;
        private float _dragOffX;
        private float _dragOffY;
        private bool  _prevLmbDown = false;

        // ── Config hot-reload ─────────────────────────────────────────────────
        private FileSystemWatcher _cfgWatcher;
        private volatile bool     _cfgDirty = false;

        // ── Brushes ───────────────────────────────────────────────────────────
        private IBrush _bgBrush;
        private IBrush _hdrBrush;
        private IBrush _borderBrush;
        private IBrush _hdrBorderBrush;
        private IBrush _rowAltBrush;
        private IBrush _curRunBrush;
        private IBrush _sepBrush;
        private IBrush _btnBrush;
        private IBrush _btnHoverBrush;
        private IBrush _btnProgressBrush;
        private IBrush _barBgBrush;
        private IBrush _barGreenBrush;
        private IBrush _barOrangeBrush;
        private IBrush _barRedBrush;

        // ── Fonts ─────────────────────────────────────────────────────────────
        private IFont _hdrFont;
        private IFont _colFont;
        private IFont _rowFont;
        private IFont _curFont;
        private IFont _goodFont;
        private IFont _warnFont;
        private IFont _normFont;
        private IFont _badFont;

        // ── File paths ────────────────────────────────────────────────────────
        private string _historyFile;
        private string _configFile;

        /// <summary>
        /// Returns a localized string for the given key based on the current _lang setting.
        /// </summary>
        private string L(string key)
        {
            switch (_lang)
            {
                case "fr":
                    switch (key)
                    {
                        case "title":      return "Historique GR";
                        case "col_time":   return "Temps";
                        case "col_result": return "Resultat";
                        case "killed":     return "✓ Tue";
                        case "timeout":    return "✗ Timeout";
                        case "waiting":    return "En attente d'un GR...";  // kept for compat
                        case "no_history": return "Aucune run terminée";
                    }
                    break;
                case "de":
                    switch (key)
                    {
                        case "title":      return "GR Verlauf";
                        case "col_time":   return "Zeit";
                        case "col_result": return "Ergebnis";
                        case "killed":     return "✓ Getoetet";
                        case "timeout":    return "✗ Timeout";
                        case "waiting":    return "Warte auf GR...";
                        case "no_history": return "Noch kein Run";
                    }
                    break;
                case "es":
                    switch (key)
                    {
                        case "title":      return "Historial GR";
                        case "col_time":   return "Tiempo";
                        case "col_result": return "Resultado";
                        case "killed":     return "✓ Matado";
                        case "timeout":    return "✗ Timeout";
                        case "waiting":    return "Esperando un GR...";
                        case "no_history": return "Sin historial";
                    }
                    break;
            }
            // Default: English
            switch (key)
            {
                case "title":      return "GR Timer History";
                case "col_time":   return "Time";
                case "col_result": return "Result";
                case "killed":     return "✓ Killed";
                case "timeout":    return "✗ Timeout";
                case "waiting":    return "Waiting for a GR...";
                case "no_history": return "No completed runs yet";
                default:           return key;
            }
        }

        // ─────────────────────────────────────────────────────────────────────

        public GRiftTimerPlugin()
        {
            Enabled = true;
        }

        public override void Load(IController hud)
        {
            base.Load(hud);

            var pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "GRiftTimer");
            var dataDir  = Path.Combine(pluginDir, "data");
            var csvDir   = Path.Combine(dataDir, "csv");
            _historyFile = Path.Combine(csvDir, "GRiftHistory.csv");
            _configFile  = Path.Combine(dataDir, "GRiftTimer.cfg");

            // Ensure both directories exist from the start
            try { Directory.CreateDirectory(dataDir); } catch { }
            try { Directory.CreateDirectory(csvDir);  } catch { }

            ToggleKeyEvent = Hud.Input.CreateKeyEvent(true, Key.T, false, false, false);
            ResetKeyEvent  = Hud.Input.CreateKeyEvent(true, Key.R, false, false, false);
            ConfigKeyEvent = Hud.Input.CreateKeyEvent(true, Key.S, false, false, false);

            _bgBrush        = Hud.Render.CreateBrush(210,  12,  12,  18, 0);
            _hdrBrush       = Hud.Render.CreateBrush(230,  28,  22,  48, 0);
            _borderBrush    = Hud.Render.CreateBrush(200, 110,  80, 170, -1);
            _hdrBorderBrush = Hud.Render.CreateBrush(120, 140, 100, 200, -1);
            _rowAltBrush    = Hud.Render.CreateBrush( 35, 255, 255, 255, 0);
            _curRunBrush    = Hud.Render.CreateBrush( 60,  60, 150,  60, 0);
            _sepBrush       = Hud.Render.CreateBrush( 70, 120,  90, 180, -1);
            _btnBrush         = Hud.Render.CreateBrush( 60,  80,  70, 100, -1);
            _btnHoverBrush    = Hud.Render.CreateBrush(160, 200, 170,  80, -1);
            _btnProgressBrush = Hud.Render.CreateBrush(220, 100, 220, 100,  0);
            _barBgBrush       = Hud.Render.CreateBrush( 80,  40,  40,  40,  0);
            _barGreenBrush    = Hud.Render.CreateBrush(220,  60, 200,  60,  0);
            _barOrangeBrush   = Hud.Render.CreateBrush(220, 220, 140,  40,  0);
            _barRedBrush      = Hud.Render.CreateBrush(220, 210,  50,  50,  0);

            _hdrFont  = Hud.Render.CreateFont("tahoma",  8.0f, 255, 220, 195, 255, true,  false, 180, 0, 0, 0, true);
            _colFont  = Hud.Render.CreateFont("tahoma",  6.5f, 180, 180, 165, 210, false, false,   0, 0, 0, 0, false);
            _rowFont  = Hud.Render.CreateFont("tahoma",  7.5f, 220, 210, 210, 210, false, false, 150, 0, 0, 0, true);
            _curFont  = Hud.Render.CreateFont("tahoma",  7.5f, 255, 255, 255, 180, true,  false, 160, 0, 0, 0, true);
            _goodFont = Hud.Render.CreateFont("tahoma",  7.5f, 230,  90, 210,  90, false, false, 150, 0, 0, 0, true);
            _warnFont = Hud.Render.CreateFont("tahoma",  7.5f, 230, 220, 140,  40, false, false, 150, 0, 0, 0, true);
            _normFont = Hud.Render.CreateFont("tahoma",  7.5f, 220, 215, 215, 215, false, false, 150, 0, 0, 0, true);
            _badFont  = Hud.Render.CreateFont("tahoma",  7.5f, 230, 210,  80,  80, false, false, 150, 0, 0, 0, true);

            LoadConfig();
            LoadHistory();
            InitCfgWatcher();
        }

        // ── Key toggle ────────────────────────────────────────────────────────

        public void OnKeyEvent(IKeyEvent keyEvent)
        {
            if (!keyEvent.IsPressed) return;
            if (ToggleKeyEvent.Matches(keyEvent))
                Visible = !Visible;
            else if (ResetKeyEvent.Matches(keyEvent))
                DoReset();
            else if (ConfigKeyEvent.Matches(keyEvent))
                OpenConfig();
        }

        private void OpenConfig()
        {
            try
            {
                // Ensure the file exists with commented default values
                if (!File.Exists(_configFile)) SaveConfig();
                Process.Start("notepad", _configFile);
            }
            catch { }
        }

        private void InitCfgWatcher()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configFile);
                if (!Directory.Exists(dir)) return;
                _cfgWatcher = new FileSystemWatcher(dir, Path.GetFileName(_configFile))
                {
                    NotifyFilter        = NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };
                _cfgWatcher.Changed += (s, e) => _cfgDirty = true;
            }
            catch { }
        }

        private void DoReset()
        {
            _history.Clear();
            _runNumber = 0;
            _lastCommittedStartTick = 0;
            _startTick   = 0;
            _pendingSave = false;
            try
            {
                if (File.Exists(_historyFile))
                {
                    if (_resetAction == "archive")
                    {
                        var stamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var dir    = Path.GetDirectoryName(_historyFile);
                        var archive = Path.Combine(dir, "GRiftHistory_" + stamp + ".csv");
                        File.Move(_historyFile, archive);
                    }
                    else
                    {
                        File.Delete(_historyFile);
                    }
                }
            }
            catch { }
            SaveConfig();
        }

        // ── State machine (game thread) ───────────────────────────────────────

        public void AfterCollect()
        {
            // Hot-reload config if the file has changed
            if (_cfgDirty) { _cfgDirty = false; LoadConfig(); }

            if (!Hud.Game.IsInGame)
            {
                // Loading screen (floor change) OR actual quit.
                // Apply the same grace logic as for a normal exit.
                // This avoids committing immediately during a floor loading screen.
                if (_wasInGR)
                {
                    _pendingSave = true;
                    _leaveTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    _wasInGR     = false;
                }
                if (_pendingSave && _startTick != 0)
                {
                    long now   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long grace = _lastPercent >= 100f ? 3000L : _floorGraceMs;
                    if (now - _leaveTimeMs > grace)
                        CommitRun();
                }
                return;
            }

            bool nowInGR = Hud.Game.SpecialArea == SpecialArea.GreaterRift;

            // ── Entering a GR ─────────────────────────────────────────────────────
            if (!_wasInGR && nowInGR)
            {
                int newStart = Hud.Game.CurrentTimedEventStartTick;
                if (newStart != 0)
                {
                    // The StartTick may change between floors of the same GR.
                    // If the rift is not completed and we return quickly → floor change.
                    bool isFloorChange = _pendingSave && _lastPercent < 100f;

                    if (isFloorChange || newStart == _startTick)
                    {
                        // Floor change or re-entry on the same run → continue
                        // DO NOT overwrite _startTick: keep the tick from floor 1
                        // so the timer keeps accumulating correctly
                        _pendingSave = false;
                    }
                    else
                    {
                        // New rift (previous rift completed or abandoned)
                        _startTick      = newStart;
                        _currentGRLevel = GetGRLevel();
                        _runNumber++;
                        _lastElapsed    = 0.0;
                        _lastPercent    = 0f;
                        _pendingSave    = false;
                    }
                }
            }

            // ── Leaving a GR → grace delay (floor change possible) ─────────────────
            if (_wasInGR && !nowInGR)
            {
                _pendingSave = true;
                _leaveTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            // Grace delay expired without re-entering → this is a true rift end
            // Completed rift (boss killed): 3s grace is enough
            // Abandoned rift / floor change: full grace (default 60s)
            if (_pendingSave && !nowInGR)
            {
                long now   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long grace = _lastPercent >= 100f ? 3000L : _floorGraceMs;
                if (now - _leaveTimeMs > grace)
                    CommitRun();
            }

            // ── Update timer ─────────────────────────────────────────────────
            if (nowInGR)
            {
                // Mid-rift reconnection
                if (_startTick == 0 && Hud.Game.CurrentTimedEventStartTick != 0)
                {
                    _startTick      = Hud.Game.CurrentTimedEventStartTick;
                    _currentGRLevel = GetGRLevel();
                    if (_runNumber == 0) _runNumber++;
                }

                if (_startTick != 0)
                    _lastElapsed = Math.Min((Hud.Game.CurrentGameTick - _startTick) / 60.0, 900.0);

                _lastPercent = (float)Hud.Game.RiftPercentage;
            }

            _wasInGR = nowInGR;
        }

        private void CommitRun()
        {
            // Guard: prevent double-saving the same run
            if (_startTick == 0 || _startTick == _lastCommittedStartTick)
            {
                _pendingSave = false;
                return;
            }
            var run = new RiftRun
            {
                Number         = _runNumber,
                GRLevel        = _currentGRLevel,
                ElapsedSeconds = _lastElapsed,
                Completed      = _lastPercent >= 100f
            };
            _history.Insert(0, run);
            if (_history.Count > _maxRuns)
                _history.RemoveAt(_history.Count - 1);
            AppendToHistory(run);
            _lastCommittedStartTick = _startTick;  // mark as saved
            // _startTick is NOT cleared → allows comparison on the next floor
            _pendingSave = false;
        }

        // ── Rendering ─────────────────────────────────────────────────────────

        public void PaintTopInGame(ClipState clipState)
        {
            if (clipState != ClipState.BeforeClip) return;
            if (!Visible) { _hoverBtn = 0; return; }
            if (Hud.Game.MapMode == MapMode.WaypointMap ||
                Hud.Game.MapMode == MapMode.ActMap      ||
                Hud.Game.MapMode == MapMode.Map) return;

            if (!_posInitialized)
            {
                if (_winX < 0) _winX = Hud.Window.Size.Width  * 0.68f;
                if (_winY < 0) _winY = Hud.Window.Size.Height * 0.10f;
                _posInitialized = true;
            }

            UpdateDrag();
            UpdateHover();

            bool inGR        = Hud.Game.SpecialArea == SpecialArea.GreaterRift;
            bool showPending  = _pendingSave && !inGR && _startTick != 0;
            int  historyRows  = _history.Count == 0 ? 1 : _history.Count;

            // Layout: HDR_H | COL_H | BAR_ROW_H | sep | historyRows*ROW_H | PAD
            float bodyH  = COL_H + BAR_ROW_H + 1f + historyRows * ROW_H + PAD;
            float totalH = HDR_H + bodyH;

            _winX = Math.Max(0, Math.Min(_winX, Hud.Window.Size.Width  - WIN_W));
            _winY = Math.Max(0, Math.Min(_winY, Hud.Window.Size.Height - totalH));

            // Background
            _hdrBrush   .DrawRectangle(_winX, _winY,         WIN_W, HDR_H);
            _bgBrush    .DrawRectangle(_winX, _winY + HDR_H, WIN_W, bodyH);
            _borderBrush.DrawRectangle(_winX, _winY,         WIN_W, totalH);

            // Title
            var tl = _hdrFont.GetTextLayout(L("title"));
            _hdrFont.DrawText(tl, _winX + PAD, _winY + (HDR_H - tl.Metrics.Height) / 2f);
            _hdrBorderBrush.DrawRectangle(_winX, _winY + HDR_H - 1, WIN_W, 1);

            // Hover buttons [T] [R] [S] — right-aligned in the header
            float btnY = _winY + (HDR_H - BTN_H) / 2f;
            float sX   = _winX + WIN_W - PAD - BTN_W;
            float rX   = sX - BTN_W - BTN_GAP;
            float tX   = rX - BTN_W - BTN_GAP;
            DrawHoverBtn("[S]", sX, btnY, 3);
            DrawHoverBtn("[R]", rX, btnY, 2);
            DrawHoverBtn("[T]", tX, btnY, 1);

            // Average time — centered between title and buttons
            var completed = _history.Where(r => r.Completed).ToList();
            if (completed.Count > 0)
            {
                double avg   = completed.Average(r => r.ElapsedSeconds);
                string avgTxt = "\u00f8 " + FormatTime(avg) + " (" + completed.Count + ")";
                IFont  avgFont = avg < _threshOrangeSec ? _goodFont
                               : avg < _threshRedSec    ? _warnFont
                               : _badFont;
                var tlAvg  = avgFont.GetTextLayout(avgTxt);
                float titleRight = _winX + PAD + tl.Metrics.Width + PAD;
                float availW     = tX - titleRight;
                float avgX       = titleRight + (availW - tlAvg.Metrics.Width) / 2f;
                avgFont.DrawText(tlAvg, avgX, _winY + (HDR_H - tlAvg.Metrics.Height) / 2f);
            }

            // Column headers
            float chy = _winY + HDR_H;
            DrawColRow(chy, "#", "GR", L("col_time"), L("col_result"),
                _colFont, _colFont, _colFont, _colFont, COL_H);
            _sepBrush.DrawRectangle(_winX, chy + COL_H - 1, WIN_W, 1);

            // ── Timer bar row (always visible) ────────────────────────────────
            float timerY = chy + COL_H;
            bool timerIdle = !inGR && !showPending;
            DrawTimerBarRow(timerY, _lastElapsed, _lastPercent, _currentGRLevel, timerIdle);

            // ── Separator ─────────────────────────────────────────────────────
            _sepBrush.DrawRectangle(_winX, timerY + BAR_ROW_H, WIN_W, 1);

            // ── History ───────────────────────────────────────────────────────
            float ry = timerY + BAR_ROW_H + 1f;

            if (_history.Count == 0)
            {
                var el = _rowFont.GetTextLayout(L("no_history"));
                _rowFont.DrawText(el, _winX + PAD, ry + (ROW_H - el.Metrics.Height) / 2f);
            }
            else
            {
                for (int i = 0; i < _history.Count; i++)
                {
                    if (i % 2 == 1)
                        _rowAltBrush.DrawRectangle(_winX, ry, WIN_W, ROW_H);

                    var run = _history[i];
                    // Time color mirrors the bar thresholds (green / orange / red)
                    IFont tf = run.ElapsedSeconds < _threshOrangeSec ? _goodFont
                             : run.ElapsedSeconds < _threshRedSec    ? _warnFont
                             : _badFont;
                    IFont rf = run.Completed ? _goodFont : _badFont;

                    DrawColRow(ry,
                        "#" + run.Number, "GR" + run.GRLevel,
                        FormatTime(run.ElapsedSeconds),
                        run.Completed ? L("killed") : L("timeout"),
                        _rowFont, _rowFont, tf, rf, ROW_H);

                    ry += ROW_H;
                }
            }
        }

        // ── Render helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Draws a single combined bar row: colored fill based on elapsed time,
        /// with GR level text on the left and time+% text on the right — all on one row.
        /// </summary>
        private void DrawTimerBarRow(float y, double elapsedSeconds, float percent, int grLevel, bool idle)
        {
            // Background
            _barBgBrush.DrawRectangle(_winX, y, WIN_W, BAR_ROW_H);

            if (!idle && elapsedSeconds > 0)
            {
                double refMax = _threshRedSec > 0 ? _threshRedSec : 1;
                float  fill  = (float)Math.Min(1.0, elapsedSeconds / refMax);

                IBrush brush;
                if      (elapsedSeconds < _threshOrangeSec) brush = _barGreenBrush;
                else if (elapsedSeconds < _threshRedSec)    brush = _barOrangeBrush;
                else                                        brush = _barRedBrush;

                brush.DrawRectangle(_winX, y, WIN_W * fill, BAR_ROW_H);
            }

            // Text on top of bar — same font as history rows so column widths match
            IFont  f        = idle ? _colFont : _rowFont;
            string numText  = idle ? "--"  : "#" + (_runNumber);
            string grText   = idle ? "--"  : "GR" + grLevel;
            string timeText = idle ? "00:00:000" : FormatTime(elapsedSeconds);
            // Time color mirrors bar thresholds
            IFont  ft       = idle ? _colFont
                            : elapsedSeconds < _threshOrangeSec ? _goodFont
                            : elapsedSeconds < _threshRedSec    ? _warnFont
                            : _badFont;
            string resText  = idle ? "%" : "  " + percent.ToString("F0") + "%";

            // # right-aligned before COL_GR (same as DrawColRow)
            var tlNum  = f.GetTextLayout(numText);
            var tlGr   = f.GetTextLayout(grText);
            var tlTime = ft.GetTextLayout(timeText);
            var tlRes  = f.GetTextLayout(resText);

            float cy = y + (BAR_ROW_H - tlGr.Metrics.Height) / 2f;
            f.DrawText(tlNum,   _winX + COL_NUM,  cy);
            f.DrawText(tlGr,    _winX + COL_GR,   cy);
            ft.DrawText(tlTime, _winX + COL_TIME,  cy);
            f.DrawText(tlRes,   _winX + COL_RES,   cy);
        }

        private void DrawColRow(float y,
            string c1, string c2, string c3, string c4,
            IFont f1, IFont f2, IFont f3, IFont f4, float h)
        {
            DrawCell(f1, c1, _winX + COL_NUM,  y, h);
            DrawCell(f2, c2, _winX + COL_GR,   y, h);
            DrawCell(f3, c3, _winX + COL_TIME, y, h);
            DrawCell(f4, c4, _winX + COL_RES,  y, h);
        }

        private void DrawCell(IFont f, string text, float x, float y, float h)
        {
            var l = f.GetTextLayout(text);
            f.DrawText(l, x, y + (h - l.Metrics.Height) / 2f);
        }

        private void DrawCellRight(IFont f, string text, float xRight, float y, float h)
        {
            var l = f.GetTextLayout(text);
            f.DrawText(l, xRight - l.Metrics.Width, y + (h - l.Metrics.Height) / 2f);
        }

        // ── Hover buttons ─────────────────────────────────────────────────────

        /// <summary>
        /// Checks if the cursor is hovering over a button long enough to trigger it.
        /// Must be called every frame from PaintTopInGame.
        /// </summary>
        private void UpdateHover()
        {
            if (_dragging) { _hoverBtn = 0; _hoverConsumed = 0; return; }

            float btnY = _winY + (HDR_H - BTN_H) / 2f;
            float sX   = _winX + WIN_W - PAD - BTN_W;
            float rX   = sX - BTN_W - BTN_GAP;
            float tX   = rX - BTN_W - BTN_GAP;

            int hoveredNow = 0;
            if (Hud.Window.CursorInsideRect(tX, btnY, BTN_W, BTN_H))      hoveredNow = 1;
            else if (Hud.Window.CursorInsideRect(rX, btnY, BTN_W, BTN_H)) hoveredNow = 2;
            else if (Hud.Window.CursorInsideRect(sX, btnY, BTN_W, BTN_H)) hoveredNow = 3;

            // Release lock once cursor leaves the consumed button
            if (_hoverConsumed != 0 && hoveredNow != _hoverConsumed)
                _hoverConsumed = 0;

            // Block re-trigger while cursor still on the button that just fired
            if (_hoverConsumed != 0)
            {
                _hoverBtn = 0;
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (hoveredNow != _hoverBtn)
            {
                _hoverBtn     = hoveredNow;
                _hoverStartMs = now;
                return;
            }

            if (hoveredNow != 0 && now - _hoverStartMs >= HOVER_MS)
            {
                _hoverConsumed = hoveredNow;   // lock until cursor leaves
                _hoverBtn      = 0;
                switch (hoveredNow)
                {
                    case 1: Visible = !Visible; break;
                    case 2: DoReset();          break;
                    case 3: OpenConfig();       break;
                }
            }
        }

        /// <summary>
        /// Draws a single hover button and its fill progress bar.
        /// </summary>
        private void DrawHoverBtn(string label, float x, float y, int btnId)
        {
            bool hovered = _hoverBtn == btnId;
            (hovered ? _btnHoverBrush : _btnBrush).DrawRectangle(x, y, BTN_W, BTN_H);

            if (hovered)
            {
                long  now      = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                float progress = Math.Min(1f, (float)(now - _hoverStartMs) / HOVER_MS);
                if (progress > 0f)
                    _btnProgressBrush.DrawRectangle(x, y + BTN_H - 3f, BTN_W * progress, 3f);
            }

            var tl = _colFont.GetTextLayout(label);
            _colFont.DrawText(tl,
                x + (BTN_W - tl.Metrics.Width)  / 2f,
                y + (BTN_H - tl.Metrics.Height) / 2f);
        }

        // ── Drag & drop ───────────────────────────────────────────────────────

        private void UpdateDrag()
        {
            bool lmbDown = (Control.MouseButtons & MouseButtons.Left) != 0;
            int  cx = Hud.Window.CursorX;
            int  cy = Hud.Window.CursorY;

            if (lmbDown && !_prevLmbDown)
            {
                if (Hud.Window.CursorInsideRect(_winX, _winY, WIN_W, HDR_H))
                {
                    _dragging = true;
                    _dragOffX = cx - _winX;
                    _dragOffY = cy - _winY;
                }
            }

            if (_dragging)
            {
                if (lmbDown)
                {
                    _winX = cx - _dragOffX;
                    _winY = cy - _dragOffY;
                }
                else
                {
                    _dragging = false;
                    SaveConfig();
                }
            }

            _prevLmbDown = lmbDown;
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        private static string FormatTime(double seconds)
        {
            int s   = (int)Math.Max(0, seconds);
            int min = s / 60;
            int sec = s % 60;
            int ms  = (int)((seconds - Math.Floor(seconds)) * 1000);
            return string.Format("{0:D2}:{1:D2}:{2:D3}", min, sec, ms);
        }

        private int GetGRLevel()
        {
            return (int)Hud.Game.Me.InGreaterRiftRank;
        }

        // ── Persistence ───────────────────────────────────────────────────────

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(_configFile)) return;
                foreach (var raw in File.ReadAllLines(_configFile))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
                    var p = line.Split('=');
                    if (p.Length != 2) continue;
                    var key = p[0].Trim();
                    var val = p[1].Trim();
                    switch (key)
                    {
                        case "x":         float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out _winX); _posInitialized = false; break;
                        case "y":         float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out _winY); _posInitialized = false; break;
                        case "runs":      int.TryParse(val, out _runNumber); break;
                        case "max_runs":      int  v; if (int.TryParse(val, out v) && v >= 1 && v <= 50) _maxRuns = v; break;
                        case "grace_ms":      long g; if (long.TryParse(val, out g) && g >= 0)           _floorGraceMs = g; break;
                        case "lang":          _lang = val.ToLowerInvariant(); break;
                        case "thresh_orange":  double to; if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out to) && to > 0) _threshOrangeSec = to; break;
                        case "thresh_red":     double tr; if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out tr) && tr > 0) _threshRedSec    = tr; break;
                        case "reset_action":   if (val == "archive" || val == "delete") _resetAction = val; break;
                    }
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllLines(_configFile, new[]
                {
                    "# GR Timer Config - editable on the fly (press S in-game)",
                    "# Save the file, changes apply immediately.",
                    "#",
                    "# lang          : display language  en | fr | de | es",
                    "# max_runs      : number of runs in history (1-50)",
                    "# grace_ms      : delay in ms before validating a rift end after leaving the zone",
                    "#               60000 = 60s  (increase if your connection is slow)",
                    "# thresh_orange : elapsed seconds before bar turns orange (default 90 = 1:30)",
                    "# thresh_red    : elapsed seconds before bar turns red    (default 100 = 1:40)",
                    "# reset_action  : what to do with the CSV when pressing [R]  archive | delete",
                    "#               archive = rename GRiftHistory.csv with timestamp (default)",
                    "#               delete  = permanently delete",
                    "#",
                    "lang="          + _lang,
                    "max_runs="      + _maxRuns.ToString(CultureInfo.InvariantCulture),
                    "grace_ms="      + _floorGraceMs.ToString(CultureInfo.InvariantCulture),
                    "thresh_orange=" + _threshOrangeSec.ToString(CultureInfo.InvariantCulture),
                    "thresh_red="    + _threshRedSec.ToString(CultureInfo.InvariantCulture),
                    "reset_action="  + _resetAction,
                    "#",
                    "# Window position (updated automatically when dragging)",
                    "x="    + _winX.ToString(CultureInfo.InvariantCulture),
                    "y="    + _winY.ToString(CultureInfo.InvariantCulture),
                    "runs=" + _runNumber.ToString(CultureInfo.InvariantCulture)
                });
            }
            catch { }
        }

        private void LoadHistory()
        {
            try
            {
                if (!File.Exists(_historyFile)) return;
                var recent = File.ReadAllLines(_historyFile)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Reverse()
                    .Take(_maxRuns);

                foreach (var line in recent)
                {
                    var p = line.Split(',');
                    if (p.Length < 4) continue;
                    int    num; if (!int.TryParse(p[0].Trim(), out num)) continue;
                    int    lvl; if (!int.TryParse(p[1].Trim(), out lvl)) continue;
                    double sec; if (!double.TryParse(p[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out sec)) continue;
                    bool   ok  = p[3].Trim() == "1";
                    _history.Add(new RiftRun { Number = num, GRLevel = lvl, ElapsedSeconds = sec, Completed = ok });
                }

                if (_history.Count > 0)
                    _runNumber = Math.Max(_runNumber, _history.Max(r => r.Number));
            }
            catch { }
        }

        private void AppendToHistory(RiftRun run)
        {
            try
            {
                var dir = Path.GetDirectoryName(_historyFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var line = string.Format(CultureInfo.InvariantCulture,
                    "{0},{1},{2:F1},{3}",
                    run.Number, run.GRLevel, run.ElapsedSeconds, run.Completed ? "1" : "0");
                File.AppendAllText(_historyFile, line + Environment.NewLine);
            }
            catch { }
        }
    }
}
