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
        private const float WIN_W_BASE  = 390f; // width without optional columns
        private const float WIN_W_PYLON  = 180f;  // extra width when pylons column is visible
        private const float HDR_H    = 32f;
        private const float COL_H    = 28f;
        private const float ROW_H    = 28f;
        private const float PAD      = 8f;

        private const float COL_NUM  = PAD;    // "#"      left at 8px
        private const float COL_GR   = 60f;   // "GR"     left at 60px  (fits "#99" before it)
        private const float COL_TIME  = 130f;  // "Time"   left at 130px (fits "GR150")
        private const float COL_RES   = 255f;  // "Result" left at 255px

        // Optional columns — each is a computed property so adding/removing columns is automatic
        // Formula: WIN_W_BASE + sum of all preceding optional column widths that are active
        private float COL_PYLON => WIN_W_BASE; // 1st optional column, always starts at WIN_W_BASE
        // Future: private float COL_XYZ => WIN_W_BASE + (_showPylons ? WIN_W_PYLON : 0f);

        private float WIN_W => WIN_W_BASE + (_showPylons ? WIN_W_PYLON : 0f);
        // Future: + (_showXyz ? WIN_W_XYZ : 0f)

        private const float BAR_ROW_H = 26f;  // height of the combined timer bar row

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
        private float  _maxPercent     = 0f;   // max RiftPercentage seen this run (latches at peak)
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

        // ── Pylon tracking (current run) ──────────────────────────────────────
        private readonly List<PylonRecord>              _currentPylonList   = new List<PylonRecord>();
        private readonly HashSet<uint>                   _seenPylonIds       = new HashSet<uint>();
        // Dedup by type+grid-position: same shrine can appear under 2 different AcdIds after activation
        private readonly HashSet<string>                 _seenPylonPositions = new HashSet<string>();
        // Cache: pylon AcdId → (TextureSno, FrameIndex) built from Hud.Game.Markers each tick
        private readonly Dictionary<uint, (uint sno, int frame)> _pylonTexCache = new Dictionary<uint, (uint, int)>();

        // ── Localization ──────────────────────────────────────────────────────
        // Supported values: en, fr, de, es
        private string _lang = "en";

        // ── Timer bar color thresholds ─────────────────────────────────────────
        private double _threshOrangeSec = 90.0;   // green below, orange above (default 1:30)
        private double _threshRedSec    = 100.0;  // orange below, red above (default 1:40)

        // ── Column visibility ─────────────────────────────────────────────
        private bool _showPylons = false;
        private bool _showStats  = true;

        // ── Debug window ─────────────────────────────────────────────
        private bool   _debugEnabled = false;
        private string _debugVars    = "pylons"; // comma-separated: pylons,shrines,markers
        private const int    DEBUG_MAX_LINES = 80;
        private const float  DEBUG_WIN_W     = 420f;
        private const float  DEBUG_ROW_H     = 18f;
        private readonly List<string>   _debugLog      = new List<string>();
        private readonly HashSet<string> _debugSeen     = new HashSet<string>(); // dedup per tick
        private string _debugLastShrineSnapshot = "";  // change detection

        // ── History ───────────────────────────────────────────────────────────
        private int    _maxRuns     = 10;
        private string _resetAction = "archive"; // "archive" or "delete"
        private readonly List<RiftRun> _history = new List<RiftRun>();

        private struct PylonRecord
        {
            public ShrineType Type;
            public uint       TextureSno;
            public int        TextureFrame;
        }

        private class RiftRun
        {
            public int              Number;
            public int              GRLevel;
            public double           ElapsedSeconds;
            public bool             Completed;
            public List<PylonRecord> Pylons = new List<PylonRecord>();
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
                        case "col_pylons": return "Pylônes";
                        case "killed":     return "✓ Tue";
                        case "timeout":    return "✗ Timeout";
                        case "waiting":    return "En attente d'un GR...";  // kept for compat
                        case "no_history": return "Aucune run terminée";
                    }
                    break;
            }
            // Default: English
            switch (key)
            {
                case "title":      return "GR Timer History";
                case "col_time":   return "Time";
                case "col_result": return "Result";
                case "col_pylons": return "Pylons";
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
            ConfigKeyEvent = Hud.Input.CreateKeyEvent(true, Key.S, true, false, true);

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
            EnsureCsvExists();
            InitCfgWatcher();
        }

        // ── Key toggle ────────────────────────────────────────────────────────

        public void OnKeyEvent(IKeyEvent keyEvent)
        {
            if (!keyEvent.IsPressed) return;
            if (ToggleKeyEvent.Matches(keyEvent))
                Visible = !Visible;
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
            _startTick      = 0;
            _pendingSave    = false;
            _maxPercent     = 0f;
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
            EnsureCsvExists();
            SaveConfig();
        }

        // ── State machine (game thread) ───────────────────────────────────────

        public void AfterCollect()
        {
            // Hot-reload config if the file has changed
            if (_cfgDirty) { _cfgDirty = false; LoadConfig(); LoadHistory(); }

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
                        _maxPercent     = 0f;
                        _pendingSave    = false;
                        _currentPylonList.Clear();
                        _seenPylonIds.Clear();
                        _seenPylonPositions.Clear();
                        _pylonTexCache.Clear();
                    }
                }
            }

            // ── Leaving a GR → grace delay (floor change possible) ─────────────────
            if (_wasInGR && !nowInGR)
            {
                _pendingSave = true;
                _leaveTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            // Grace delay expired without re-entering → this is a true rift end.
            // Here IsInGame=true and nowInGR=false: player is in town/act, floor changes
            // always go through a loading screen (IsInGame=false), so 3s is enough.
            if (_pendingSave && !nowInGR)
            {
                long now   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (now - _leaveTimeMs > 3000L)
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
                if (_lastPercent > _maxPercent) _maxPercent = _lastPercent;

                // Update texture cache for pylons still visible (not yet activated)
                foreach (var marker in Hud.Game.Markers)
                {
                    if (!marker.IsPylon || marker.TextureSno == 0) continue;
                    // Match marker to shrine by proximity
                    var shrine = Hud.Game.Shrines.FirstOrDefault(s =>
                        s.IsPylon && !s.IsOperated && !s.IsDisabled &&
                        s.FloorCoordinate.XYDistanceTo(marker.FloorCoordinate) <= 5f);
                    if (shrine != null)
                        _pylonTexCache[shrine.AcdId] = (marker.TextureSno, marker.TextureFrameIndex);
                }

                // Track pylons activated this run
                foreach (var shrine in Hud.Game.Shrines)
                {
                    if (shrine.IsPylon && shrine.IsOperated && !shrine.IsDisabled)
                    {
                        _seenPylonIds.Add(shrine.AcdId);
                        // Guard against same shrine appearing under 2 AcdIds: dedup by type + grid cell
                        string posKey = shrine.Type + "_"
                            + (int)(shrine.FloorCoordinate.X / 10f) + "_"
                            + (int)(shrine.FloorCoordinate.Y / 10f);
                        if (_seenPylonPositions.Add(posKey))
                        {
                            (uint sno, int frame) tex;
                            _pylonTexCache.TryGetValue(shrine.AcdId, out tex);
                            _currentPylonList.Add(new PylonRecord
                            {
                                Type         = shrine.Type,
                                TextureSno   = tex.sno,
                                TextureFrame = tex.frame
                            });
                        }
                    }
                }

                // ── Debug logging ───────────────────────────────────────────
                if (_debugEnabled)
                    CollectDebugData();
            }
            else if (_debugEnabled)
            {
                // Outside GR: still log shrine/marker state if requested
                CollectDebugData();
            }

            _wasInGR = nowInGR;
        }

        // ── Debug data collection ──────────────────────────────────────────────
        private void CollectDebugData()
        {
            if (!_debugEnabled || !Hud.Game.IsInGame) return;
            var vars = _debugVars.Split(new[]{',','|',' '}, System.StringSplitOptions.RemoveEmptyEntries);
            bool inGR = Hud.Game.SpecialArea == SpecialArea.GreaterRift;
            string ts = DateTime.Now.ToString("HH:mm:ss");

            foreach (var v in vars)
            {
                string key = v.Trim().ToLowerInvariant();

                // ── pylons (activated this run) ────────────────────────────────
                if (key == "pylons" || key == "current_pylons")
                {
                    string snap = string.Join("|", _currentPylonList.Select(p => p.Type.ToString()));
                    if (snap != _debugLastShrineSnapshot)
                    {
                        _debugLastShrineSnapshot = snap;
                        DebugLog($"[{ts}] pylons ({_currentPylonList.Count}): {(snap.Length > 0 ? snap : "(none)")}");
                    }
                }

                // ── shrines (all in world, live snapshot) ─────────────────────
                else if (key == "shrines")
                {
                    var shrines = Hud.Game.Shrines.ToList();
                    string snap = string.Join("|", shrines.Select(s =>
                        $"{s.Type}:op={s.IsOperated}:pyl={s.IsPylon}:dis={s.IsDisabled}:id={s.AcdId}"));
                    if (snap != _debugLastShrineSnapshot)
                    {
                        _debugLastShrineSnapshot = snap;
                        DebugLog($"[{ts}] shrines ({shrines.Count}): --- change detected ---");
                        foreach (var s in shrines)
                            DebugLog($"  {s.Type,-20} pylon={s.IsPylon} operated={s.IsOperated} disabled={s.IsDisabled} id={s.AcdId}");
                    }
                }

                // ── markers (minimap markers, pylon textures) ─────────────────
                else if (key == "markers")
                {
                    var pylMarkers = Hud.Game.Markers.Where(m => m.IsPylon).ToList();
                    string snap = string.Join("|", pylMarkers.Select(m => $"{m.TextureSno}:{m.TextureFrameIndex}"));
                    string snapKey = "markers:" + snap;
                    if (!_debugSeen.Contains(snapKey))
                    {
                        _debugSeen.Add(snapKey);
                        DebugLog($"[{ts}] markers/pylon ({pylMarkers.Count})");
                        foreach (var m in pylMarkers)
                            DebugLog($"  sno={m.TextureSno} frame={m.TextureFrameIndex} isPylon={m.IsPylon}");
                    }
                }

                // ── rift_state (generic timer/percent) ───────────────────────
                else if (key == "rift_state")
                {
                    string line = $"[{ts}] rift inGR={inGR} pct={_lastPercent:F1}% elapsed={_lastElapsed:F2}s tick={_startTick}";
                    string lineKey = "rift_state:" + line;
                    if (!_debugSeen.Contains(lineKey))
                    {
                        _debugSeen.Add(lineKey);
                        DebugLog(line);
                    }
                }

                // ── unknown variable name → show hint ─────────────────────────
                else
                {
                    string hint = $"[debug] unknown var '{v}' — available: pylons, shrines, markers, rift_state";
                    if (!_debugSeen.Contains(hint)) { _debugSeen.Add(hint); DebugLog(hint); }
                }
            }

            // Reset per-tick seen set (keeps only structural-change dedup)
            _debugSeen.Clear();
        }

        private void DebugLog(string line)
        {
            _debugLog.Add(line);
            if (_debugLog.Count > DEBUG_MAX_LINES)
                _debugLog.RemoveAt(0);
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
                Completed      = _maxPercent >= 99f,
                Pylons         = new List<PylonRecord>(_currentPylonList)
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

            // Layout: HDR_H | COL_H | BAR_ROW_H | sep | historyRows*ROW_H | PAD | STATS_ROW
            float bodyH  = COL_H + BAR_ROW_H + 1f + historyRows * ROW_H + (_showStats ? PAD + ROW_H : 0f);
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

            // Average time — removed from header, shown in stats box below history

            // Column headers
            float chy = _winY + HDR_H;
            DrawColRow(chy, "#", "GR", L("col_time"), L("col_result"),
                _colFont, _colFont, _colFont, _colFont, COL_H);
            if (_showPylons)
                DrawCell(_colFont, L("col_pylons"), _winX + COL_PYLON, chy, COL_H);
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
                ry += ROW_H;
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

                    string pylonsStr = run.Pylons.Count > 0
                        ? string.Join(" ", run.Pylons.Select(p => PylonDisplayName(p.Type)))
                        : "";
                    DrawColRow(ry,
                        "#" + run.Number, "GR" + run.GRLevel,
                        FormatTime(run.ElapsedSeconds),
                        run.Completed ? L("killed") : L("timeout"),
                        _rowFont, _rowFont, tf, rf, ROW_H);
                    if (_showPylons && pylonsStr.Length > 0)
                        DrawCell(_normFont, pylonsStr, _winX + COL_PYLON, ry, ROW_H);

                    ry += ROW_H;
                }
            }

            // ── Stats box (below history) ─────────────────────────────────────
            if (_showStats)
            {
                float statsY = ry + PAD;
                float statsH = ROW_H;
                _bgBrush    .DrawRectangle(_winX, statsY, WIN_W, statsH);
                _borderBrush.DrawRectangle(_winX, statsY, WIN_W, statsH);
                var completedRuns = _history.Where(r => r.Completed).ToList();
                if (completedRuns.Count > 0)
                {
                    double total   = completedRuns.Sum(r => r.ElapsedSeconds);
                    double avg     = total / completedRuns.Count;
                    IFont  avgFont = avg < _threshOrangeSec ? _goodFont
                                   : avg < _threshRedSec    ? _warnFont
                                   : _badFont;
                    string avgTxt  = "\u00f8 " + FormatTime(avg) + " (" + completedRuns.Count + ")  \u03a3 " + FormatTime(total) + " / " + completedRuns.Count;
                    var tlAvg = avgFont.GetTextLayout(avgTxt);
                    avgFont.DrawText(tlAvg, _winX + PAD, statsY + (statsH - tlAvg.Metrics.Height) / 2f);
                }
                else
                {
                    var tlNo = _colFont.GetTextLayout("\u00f8 --:--:---");
                    _colFont.DrawText(tlNo, _winX + PAD, statsY + (statsH - tlNo.Metrics.Height) / 2f);
                }
            }

            // ── Pylons legend tooltip ─────────────────────────────────────────────────
            if (_showPylons)
                DrawPylonTooltip();

            // ── Debug window ───────────────────────────────────────────────────────────
            if (_debugEnabled)
                DrawDebugWindow();
        }

        // ── Render helpers ───────────────────────────────────────────────────────────

        private void DrawPylonTooltip()
        {
            float colHdrX = _winX + COL_PYLON;
            float colHdrY = _winY + HDR_H;
            if (!Hud.Window.CursorInsideRect(colHdrX, colHdrY, WIN_W_PYLON, COL_H)) return;

            string[] abbrevs = _lang == "fr"
                ? new[] { "Cond", "Puis", "Boucl", "Canal", "Vit" }
                : new[] { "Cond", "Pow",  "Shld",  "Chan",  "Spd" };
            string[] names = _lang == "fr"
                ? new[] { "Pyl\u00f4ne de Conduction", "Pyl\u00f4ne de Puissance",
                          "Pyl\u00f4ne de Bouclier",   "Pyl\u00f4ne de Canalisation", "Pyl\u00f4ne de Vitesse" }
                : new[] { "Conduit Pylon", "Power Pylon", "Shield Pylon", "Channeling Pylon", "Speed Pylon" };

            const float TT_PAD   = 6f;
            const float TT_ROW   = 20f;
            const float COL_ARR  = 58f;   // x offset for arrow (room for longest abbrev "Boucl")
            const float COL_NAME = 78f;   // x offset for full name
            float ttW = WIN_W;
            float ttH = TT_PAD * 2 + abbrevs.Length * TT_ROW;
            float ttX = _winX;
            float ttY = _winY - ttH - 2f;   // above the main window header

            _bgBrush.DrawRectangle(ttX, ttY, ttW, ttH);
            _borderBrush.DrawRectangle(ttX, ttY, ttW, ttH);

            for (int i = 0; i < abbrevs.Length; i++)
            {
                float rowY = ttY + TT_PAD + i * TT_ROW;
                var tlA  = _normFont.GetTextLayout(abbrevs[i]);
                var tlAr = _normFont.GetTextLayout("\u2192");
                var tlN  = _normFont.GetTextLayout(names[i]);
                float mid = rowY + (TT_ROW - tlA.Metrics.Height) / 2f;
                _normFont.DrawText(tlA,  ttX + TT_PAD,   mid);
                _normFont.DrawText(tlAr, ttX + COL_ARR,  mid);
                _normFont.DrawText(tlN,  ttX + COL_NAME, mid);
            }
        }

        private void DrawDebugWindow()
        {
            float dbgX2 = _winX + WIN_W + 12f;
            float dbgY2 = _winY;

            if (_debugLog.Count == 0)
            {
                float dbgH = HDR_H + DEBUG_ROW_H + PAD;
                _bgBrush.DrawRectangle(dbgX2, dbgY2, DEBUG_WIN_W, dbgH);
                _borderBrush.DrawRectangle(dbgX2, dbgY2, DEBUG_WIN_W, dbgH);
                _hdrBrush.DrawRectangle(dbgX2, dbgY2, DEBUG_WIN_W, HDR_H);
                var tl0 = _hdrFont.GetTextLayout("[DEBUG] waiting for data\u2026");
                _hdrFont.DrawText(tl0, dbgX2 + PAD, dbgY2 + (HDR_H - tl0.Metrics.Height) / 2f);
                _hdrBorderBrush.DrawRectangle(dbgX2, dbgY2 + HDR_H - 1, DEBUG_WIN_W, 1);
                DrawHoverBtn("[X]", dbgX2 + DEBUG_WIN_W - PAD - BTN_W, dbgY2 + (HDR_H - BTN_H) / 2f, 4);
                return;
            }

            int    visibleLines = Math.Min(_debugLog.Count, 40);
            float  dbgWinH      = HDR_H + DEBUG_ROW_H + visibleLines * DEBUG_ROW_H + PAD;

            _bgBrush.DrawRectangle(dbgX2, dbgY2, DEBUG_WIN_W, dbgWinH);
            _borderBrush.DrawRectangle(dbgX2, dbgY2, DEBUG_WIN_W, dbgWinH);

            _hdrBrush.DrawRectangle(dbgX2, dbgY2, DEBUG_WIN_W, HDR_H);
            string hdr2 = "[DEBUG] " + _debugVars + "  (" + _debugLog.Count + "/" + DEBUG_MAX_LINES + " lines)";
            var tlHdr = _hdrFont.GetTextLayout(hdr2);
            _hdrFont.DrawText(tlHdr, dbgX2 + PAD, dbgY2 + (HDR_H - tlHdr.Metrics.Height) / 2f);
            _hdrBorderBrush.DrawRectangle(dbgX2, dbgY2 + HDR_H - 1, DEBUG_WIN_W, 1);
            DrawHoverBtn("[X]", dbgX2 + DEBUG_WIN_W - PAD - BTN_W, dbgY2 + (HDR_H - BTN_H) / 2f, 4);
            // Diagnostic row: cursor + window position + drag state
            string diag = "cx=" + Hud.Window.CursorX + " cy=" + Hud.Window.CursorY
                + "  winX=" + (int)_winX + " winY=" + (int)_winY
                + "  drag=" + _dragging;
            var tlDiag2 = _normFont.GetTextLayout(diag);
            _normFont.DrawText(tlDiag2, dbgX2 + PAD,
                dbgY2 + HDR_H + 2f + (DEBUG_ROW_H - tlDiag2.Metrics.Height) / 2f);

            int   startIdx = Math.Max(0, _debugLog.Count - Math.Max(1, visibleLines - 1));
            float lineY    = dbgY2 + HDR_H + DEBUG_ROW_H + 2f;
            for (int i = startIdx; i < _debugLog.Count; i++)
            {
                if ((i - startIdx) % 2 == 1)
                    _rowAltBrush.DrawRectangle(dbgX2, lineY, DEBUG_WIN_W, DEBUG_ROW_H);
                string text = _debugLog[i];
                if (text.Length > 80) text = text.Substring(0, 77) + "\u2026";
                var tl = _normFont.GetTextLayout(text);
                _normFont.DrawText(tl, dbgX2 + PAD, lineY + (DEBUG_ROW_H - tl.Metrics.Height) / 2f);
                lineY += DEBUG_ROW_H;
            }
        }

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
            if (!idle && _showPylons && _currentPylonList.Count > 0)
            {
                string pylonText = string.Join(" ", _currentPylonList.Select(p => PylonDisplayName(p.Type)));
                _normFont.DrawText(_normFont.GetTextLayout(pylonText), _winX + COL_PYLON, cy);
            }
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

            // [X] clear button on the debug window header
            if (hoveredNow == 0 && _debugEnabled)
            {
                float dbgX2  = _winX + WIN_W + 12f;
                float xBtnX2 = dbgX2 + DEBUG_WIN_W - PAD - BTN_W;
                float xBtnY2 = _winY + (HDR_H - BTN_H) / 2f;
                if (Hud.Window.CursorInsideRect(xBtnX2, xBtnY2, BTN_W, BTN_H)) hoveredNow = 4;
            }

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
                    case 4: _debugLog.Clear(); _debugSeen.Clear(); _debugLastShrineSnapshot = ""; break;
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
                        case "show_pylons":    _showPylons = val.ToLowerInvariant() != "no"; break;
                        case "show_stats":     _showStats  = val.ToLowerInvariant() != "no"; break;
                        case "debug":          _debugEnabled = val.ToLowerInvariant() == "yes"; break;
                        case "debug_vars":     _debugVars = val.Trim(); break;
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
                    "# lang          : display language  en | fr",
                    "# max_runs      : number of runs in history (1-50)",
                    "# grace_ms      : delay in ms before validating a rift end after leaving the zone",
                    "#               60000 = 60s  (increase if your connection is slow)",
                    "# thresh_orange : elapsed seconds before bar turns orange (default 90 = 1:30)",
                    "# thresh_red    : elapsed seconds before bar turns red    (default 100 = 1:40)",
                    "# reset_action  : what to do with the CSV when pressing [R]  archive | delete",
                    "#               archive = rename GRiftHistory.csv with timestamp (default)",
                    "#               delete  = permanently delete",
                    "# show_pylons   : show the Pylons column  yes | no",
                    "# show_stats    : show the stats box below history  yes | no",
                    "# debug         : show debug window  yes | no",
                    "# debug_vars    : variables to watch  pylons | shrines | markers | rift_state",
                    "#               multiple vars: debug_vars=pylons,shrines",
                    "#",
                    "lang="          + _lang,
                    "max_runs="      + _maxRuns.ToString(CultureInfo.InvariantCulture),
                    "grace_ms="      + _floorGraceMs.ToString(CultureInfo.InvariantCulture),
                    "thresh_orange=" + _threshOrangeSec.ToString(CultureInfo.InvariantCulture),
                    "thresh_red="    + _threshRedSec.ToString(CultureInfo.InvariantCulture),
                    "reset_action="  + _resetAction,
                    "show_pylons="   + (_showPylons ? "yes" : "no"),
                    "show_stats="    + (_showStats  ? "yes" : "no"),
                    "debug="         + (_debugEnabled ? "yes" : "no"),
                    "debug_vars="    + _debugVars,
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
                _history.Clear();
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
                    var pylons = new List<PylonRecord>();
                    if (p.Length >= 5 && !string.IsNullOrWhiteSpace(p[4]))
                    {
                        foreach (var entry in p[4].Trim().Split('|'))
                        {
                            // Format: sno:frame:key  OR legacy: key
                            var parts = entry.Split(':');
                            ShrineType st;
                            uint  sno   = 0;
                            int   frame = 0;
                            if (parts.Length >= 3)
                            {
                                uint.TryParse(parts[0], out sno);
                                int.TryParse(parts[1], out frame);
                                AbbrToShrineType(parts[2], out st);
                            }
                            else if (!AbbrToShrineType(parts[0], out st)) continue;
                            pylons.Add(new PylonRecord { Type = st, TextureSno = sno, TextureFrame = frame });
                        }
                    }
                    _history.Add(new RiftRun { Number = num, GRLevel = lvl, ElapsedSeconds = sec, Completed = ok, Pylons = pylons });
                }

                if (_history.Count > 0)
                    _runNumber = Math.Max(_runNumber, _history.Max(r => r.Number));
            }
            catch { }
        }

        // Localized display name (type only, no "Pylon" suffix)
        private string PylonDisplayName(ShrineType t)
        {
            switch (t)
            {
                case ShrineType.ConduitPylon:    return _lang == "fr" ? "Cond"  : "Cond";
                case ShrineType.PowerPylon:      return _lang == "fr" ? "Puis"  : "Pow";
                case ShrineType.ShieldPylon:     return _lang == "fr" ? "Boucl" : "Shld";
                case ShrineType.ChannelingPylon: return _lang == "fr" ? "Canal" : "Chan";
                case ShrineType.SpeedPylon:      return _lang == "fr" ? "Vit"   : "Spd";
                default:                         return t.ToString();
            }
        }

        // CSV key: short ASCII-only code (no icons)
        private static string PylonCsvKey(ShrineType t)
        {
            switch (t)
            {
                case ShrineType.ChannelingPylon: return "Ch";
                case ShrineType.ConduitPylon:    return "Co";
                case ShrineType.PowerPylon:      return "Po";
                case ShrineType.ShieldPylon:     return "Sh";
                case ShrineType.SpeedPylon:      return "Spd";
                default:                         return "?";
            }
        }

        private static bool AbbrToShrineType(string abbr, out ShrineType result)
        {
            switch (abbr)
            {
                case "Ch":  result = ShrineType.ChannelingPylon; return true;
                case "Co":  result = ShrineType.ConduitPylon;    return true;
                case "Po":  result = ShrineType.PowerPylon;      return true;
                case "Sh":  result = ShrineType.ShieldPylon;     return true;
                case "Spd": result = ShrineType.SpeedPylon;      return true;
                // legacy (before icon update)
                case "Sp":  result = ShrineType.SpeedPylon;      return true;
                default:    result = ShrineType.BlessedShrine;   return false;
            }
        }

        private void EnsureCsvExists()
        {
            try
            {
                if (!File.Exists(_historyFile))
                    File.WriteAllText(_historyFile, string.Empty);
            }
            catch { }
        }

        private void AppendToHistory(RiftRun run)
        {
            try
            {
                var dir = Path.GetDirectoryName(_historyFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string pylonsCsv = run.Pylons.Count > 0
                    ? string.Join("|", run.Pylons.Select(p => p.TextureSno + ":" + p.TextureFrame + ":" + PylonCsvKey(p.Type)))
                    : "";
                var line = string.Format(CultureInfo.InvariantCulture,
                    "{0},{1},{2:F1},{3},{4}",
                    run.Number, run.GRLevel, run.ElapsedSeconds, run.Completed ? "1" : "0", pylonsCsv);
                File.AppendAllText(_historyFile, line + Environment.NewLine);
            }
            catch { }
        }
    }
}
