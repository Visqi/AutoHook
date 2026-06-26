using System.IO;
using AutoHook.Replay;
using Lumina.Excel.Sheets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using System.Numerics;

namespace AutoHook.Ui;

public sealed class ReplayDetailsWindow : Window, IDisposable {
    private readonly FishingReplay _replay;
    private readonly ReplayPlayer _player;
    private readonly IReadOnlyList<ReplayTimelineMarker> _markers;
    private readonly IReadOnlyList<ReplayTimelineSpan> _spans;
    private readonly DateTime _first;
    private readonly DateTime _last;
    private DateTime _playTime;
    private DateTime _prevFrame = DateTime.UtcNow;
    private float _playSpeed;

    public DateTime CurrentTime {
        get => _playTime;
        set => SeekTo(value);
    }

    public ReplayDetailsWindow(FishingReplay replay, DateTime? initialTime) : base($"Replay: {Path.GetFileName(replay.SourcePath)}###{replay.SourcePath}") {
        Service.WindowSystem.AddWindow(this);
        Size = (ImGui.GetMainViewport().Size - new Vector2(150, 150)) / ImGuiHelpers.GlobalScale;
        SizeCondition = ImGuiCond.FirstUseEver;

        _replay = replay;
        _player = new ReplayPlayer(replay);
        _markers = ReplayTimelineMarkers.Build(replay);
        _spans = ReplayTimelineMarkers.BuildSpans(replay);
        _first = replay.StartTime;
        _last = replay.EndTime;
        _playTime = initialTime ?? _first;
        if (_playTime != default)
            SeekTo(_playTime);
    }

    public void Dispose() => Service.WindowSystem.RemoveWindow(this);

    public override void Draw() {
        if (!IsOpen)
            return;

        try {
            var frameNow = DateTime.UtcNow;
            var frameDelta = frameNow - _prevFrame;
            _prevFrame = frameNow;

            if (_playSpeed > 0 && _last > _first) {
                var next = _playTime + TimeSpan.FromSeconds(frameDelta.TotalSeconds * _playSpeed);
                if (next >= _last) {
                    next = _last;
                    _playSpeed = 0;
                }
                AdvancePlayback(next);
            }

            DrawControlRow();
            DrawTimelineRow();
            DrawTimelineLegend();
            DrawSummary();

            if (ImGui.CollapsingHeader("Condition Evals", ImGuiTreeNodeFlags.DefaultOpen))
                DrawDecisions();
            if (ImGui.CollapsingHeader("WorldState", ImGuiTreeNodeFlags.DefaultOpen))
                DrawWorldStatePanel();
        }
        catch (Exception e) {
            Svc.Log.Error($"[ReplayDetails] {e.Message}");
        }
    }

    private void DrawWorldStatePanel() => TabDebug.DrawWorldState(_player.WorldState, "replay");

    private void DrawControlRow() {
        ImGui.Text($"{_playTime:HH:mm:ss.fff}");
        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.StepBackward, "5s"))
            SeekTo(_playTime - TimeSpan.FromSeconds(5));
        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.StepBackward, "1s"))
            SeekTo(_playTime - TimeSpan.FromSeconds(1));
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.StepBackward))
            StepBackFrame();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Back 1 step");
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(_playSpeed == 0 ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause))
            _playSpeed = _playSpeed == 0 ? 1 : 0;
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.StepForward))
            StepForwardFrame();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Forward 1 step");
        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "0.25x"))
            _playSpeed = 0.25f;
        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "1x"))
            _playSpeed = 1;
        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "4x"))
            _playSpeed = 4;
    }

    private void DrawTimelineRow() {
        if (_last <= _first) {
            ImGui.TextDisabled("(empty replay)");
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();
        var w = ImGui.GetWindowWidth() - 2 * ImGui.GetCursorPosX() - 15;
        var lineY = cursor.Y + 8;
        var bandTop = lineY - 6;
        var bandBottom = lineY + 6;
        var lineStart = new Vector2(cursor.X, lineY);

        foreach (var span in _spans)
            DrawSpan(dl, span, lineStart, w, _first, _last, bandTop, bandBottom);

        dl.AddLine(lineStart, lineStart + new Vector2(w, 0), 0xff00ffff);

        var frac = (float)((_playTime - _first) / (_last - _first));
        var curp = lineStart + new Vector2(w * frac, 0);
        dl.AddTriangleFilled(curp, curp + new Vector2(3, 5), curp + new Vector2(-3, 5), 0xff00ffff);

        foreach (var m in _markers)
            DrawMarker(dl, m, lineStart, w, _first, _last);

        ImGui.Dummy(new Vector2(w, 16));
        var hoverPos = ImGui.GetIO().MousePos;
        if (ImGui.IsItemHovered()) {
            if (ImGui.IsWindowFocused() && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                if (Math.Abs(hoverPos.Y - lineY) <= 8 && hoverPos.X >= lineStart.X && hoverPos.X <= lineStart.X + w) {
                    var t = _first + TimeSpan.FromTicks((long)((hoverPos.X - lineStart.X) / w * (_last - _first).Ticks));
                    SeekTo(t);
                }
            }

            ReplayTimelineMarker? hit = null;
            var bestDist = float.MaxValue;
            foreach (var m in _markers) {
                var mx = lineStart.X + w * (float)((m.Time - _first) / (_last - _first));
                var dist = Math.Abs(hoverPos.X - mx);
                if (dist <= 6 && dist < bestDist) {
                    bestDist = dist;
                    hit = m;
                }
            }
            if (hit != null) {
                var tip = $"{hit.Time:HH:mm:ss.fff}\n{hit.Label}";
                if (!string.IsNullOrEmpty(hit.TooltipExtra))
                    tip += $"\n{hit.TooltipExtra}";
                ImGui.SetTooltip(tip);
            }
            else if (Math.Abs(hoverPos.Y - lineY) <= 8)
                ImGui.SetTooltip("Scrub timeline");
        }
    }

    private static void DrawSpan(ImDrawListPtr dl, ReplayTimelineSpan span, Vector2 lineStart, float w, DateTime first, DateTime last, float bandTop, float bandBottom) {
        if (last <= first || span.End <= span.Start)
            return;
        var t0 = (float)((span.Start - first).Ticks / (double)(last - first).Ticks);
        var t1 = (float)((span.End - first).Ticks / (double)(last - first).Ticks);
        if (t1 <= t0)
            return;
        var x0 = lineStart.X + w * t0;
        var x1 = lineStart.X + w * t1;
        dl.AddRectFilled(new Vector2(x0, bandTop), new Vector2(x1, bandBottom), span.Color);
    }

    private static void DrawMarker(ImDrawListPtr dl, ReplayTimelineMarker m, Vector2 lineStart, float w, DateTime first, DateTime last) {
        if (last <= first)
            return;
        var off = (float)((m.Time - first) / (last - first));
        var center = lineStart + new Vector2(w * off, 0);
        dl.AddCircleFilled(center, 3, m.Color);
    }

    private void DrawTimelineLegend() {
        ImGui.TextDisabled("Bands: ");
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.ParsedBlue, "■ intuition");
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudViolet, "■ spectral");
        ImGui.TextDisabled("Markers: ");
        ImGui.SameLine();
        ImGui.TextDisabled("● session");
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.ParsedOrange, "● bite");
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudViolet, "● catch");
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudOrange, "● bait");
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.ParsedGreen, "● action");
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.ParsedBlue, "● preset");
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudYellow, "● decision");
    }

    private void DrawSummary() {
        var span = (_last - _first).TotalSeconds;
        ImGui.Text($"Duration: {span:F1}s  |  Ops: {_replay.Ops.Count}  |  Decisions: {_replay.Decisions.Count}");
        if (!string.IsNullOrEmpty(_replay.Metadata.PresetName))
            ImGui.Text($"Preset: {_replay.Metadata.PresetName}");
        if (_replay.Metadata.TerritoryId != 0)
            ImGui.Text($"Territory: {(_replay.Metadata.TerritoryId == 0 ? "-" : TerritoryType.GetRow(_replay.Metadata.TerritoryId).PlaceName.Value.Name.ToString())}");

        var hasSnapshot = !string.IsNullOrEmpty(_replay.Metadata.PresetSnapshotJson);
        using (ImRaii.Disabled(!hasSnapshot)) {
            if (ImGui.Button("Import replay preset")) {
                if (ReplayPresetImport.TryImport(_replay, out var error))
                    Notify.Success($"Imported preset from replay.");
                else
                    Notify.Error(error ?? "Failed to import preset.");
            }
        }
        if (!hasSnapshot) {
            ImGui.SameLine();
            ImGui.TextDisabled("(no preset snapshot)");
        }
    }

    private void DrawDecisions() {
        var cur = _playTime;
        var decisions = _replay.Decisions
            .Select(d => (Decision: d, Delta: Math.Abs((d.Timestamp - cur).TotalSeconds)))
            .Where(x => x.Delta <= 5)
            .OrderBy(x => x.Delta)
            .Take(40)
            .Select(x => x.Decision)
            .ToList();

        if (decisions.Count == 0) {
            ImGui.TextDisabled("nothing happened within 5s of scrub position");
        }
        else {
            using var table = ImRaii.Table("replay_decisions", 5,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable,
                new Vector2(0, 200.Scaled()));
            if (table) {
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 100.Scaled());
                ImGui.TableSetupColumn("Context");
                ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 140.Scaled());
                ImGui.TableSetupColumn("Detail");
                ImGui.TableSetupColumn("Conditions");
                ImGui.TableHeadersRow();

                foreach (var d in decisions) {
                    var atPlayhead = Math.Abs((d.Timestamp - cur).TotalMilliseconds) <= 250;
                    if (atPlayhead)
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.ColorConvertFloat4ToU32(new Vector4(0.25f, 0.35f, 0.5f, 0.35f)));

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(d.Timestamp.ToString("HH:mm:ss.fff"));
                    ImGui.TableNextColumn();
                    ImGui.Text($"{d.Context} / {d.PresetName}");
                    ImGui.TableNextColumn();
                    ImGui.Text(d.Action);
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped(string.IsNullOrEmpty(d.Detail) ? "-" : d.Detail);
                    ImGui.TableNextColumn();
                    if (d.ConditionResults.Count == 0) {
                        ImGui.TextDisabled("-");
                    }
                    else {
                        foreach (var (label, result) in d.ConditionResults) {
                            var color = result ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed;
                            ImGui.TextColored(color, $"{label}: {(result ? "T" : "F")}");
                        }
                    }
                }
            }
        }
    }

    private void AdvancePlayback(DateTime target) {
        if (target < _player.CurrTimestamp())
            _player.Reset();
        _player.AdvanceTo(target);
        _playTime = target;
    }

    private void SeekTo(DateTime t) {
        if (_last <= _first)
            return;
        if (t < _first)
            t = _first;
        if (t > _last)
            t = _last;
        _player.SeekTo(t);
        _playTime = t;
        _playSpeed = 0;
    }

    private void StepBackFrame() {
        var prev = _player.PrevTimestamp();
        SeekTo(prev != default ? prev : _first);
    }

    private void StepForwardFrame() {
        if (_player.TickForward())
            _playTime = _player.CurrTimestamp();
        else
            _playTime = _last;
        _playSpeed = 0;
    }
}
