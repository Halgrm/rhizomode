#!/usr/bin/env bash
#
# verify-asmdef-boundaries.sh — Phase 0.5 雛形 (Plan v5.3)
#
# 目的:
#   rhizomode の 48 asmdef + Tests 12 + Editor 1 の references が
#   Boundary CI rules に違反していないか検証する。
#
# 有効化:
#   - Phase 0.5: SKELETON_ONLY=1 で雛形のみ動作 (常に exit 0)
#   - Phase 1G: SKELETON_ONLY=0 にして実 rules を有効化、違反で exit 1
#
# Usage:
#   bash scripts/verify-asmdef-boundaries.sh                # 全 rules 検証
#   bash scripts/verify-asmdef-boundaries.sh --skeleton     # 雛形モード (常に成功)
#   SKELETON_ONLY=1 bash scripts/verify-asmdef-boundaries.sh
#
# Output:
#   - 各 rule の pass/fail を stdout に出力
#   - 違反があれば exit 1、すべて pass なら exit 0
#
# 参照:
#   - C:\Users\kiuog\.claude\plans\eager-conjuring-eich.md (v5.3) Boundary CI セクション
#   - Editor/BoundaryViolationValidator.cs (Editor 内同等チェック)

set -euo pipefail

# -----------------------------------------------------------------------------
# Configuration
# -----------------------------------------------------------------------------

SKELETON_ONLY="${SKELETON_ONLY:-1}"  # Phase 0.5 既定: 雛形モード
if [[ "${1:-}" == "--skeleton" ]]; then
    SKELETON_ONLY=1
fi
if [[ "${1:-}" == "--enable" ]]; then
    SKELETON_ONLY=0
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ASSETS_RUNTIME="${REPO_ROOT}/rhizomode/Assets/Runtime"
ASSETS_TESTS="${REPO_ROOT}/rhizomode/Assets/Tests"
ASSETS_EDITOR="${REPO_ROOT}/rhizomode/Assets/Editor"

FAIL_COUNT=0
PASS_COUNT=0

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------

# find_asmdef <name> — asmdef ファイルの絶対パスを返す
find_asmdef() {
    local name="$1"
    find "${ASSETS_RUNTIME}" "${ASSETS_TESTS}" "${ASSETS_EDITOR}" \
         -name "${name}.asmdef" -type f 2>/dev/null | head -1
}

# asmdef_refs <path> — asmdef の references フィールドから依存名を抽出 (jq 不要、grep ベース)
asmdef_refs() {
    local path="$1"
    [[ -f "${path}" ]] || return 0
    # "references": [...] の中身を抽出
    awk '
        /"references"/ { in_refs=1; next }
        in_refs && /\]/ { in_refs=0 }
        in_refs && /"[^"]+"/ {
            match($0, /"([^"]+)"/, arr)
            if (arr[1] != "") print arr[1]
        }
    ' "${path}"
}

# check_asmdef_no_ref <asmdef_name> <forbidden_ref> — 指定 asmdef が forbidden_ref を参照しないことを確認
check_asmdef_no_ref() {
    local name="$1"
    local forbidden="$2"
    local label="${3:-${name} must not reference ${forbidden}}"

    local path
    path="$(find_asmdef "${name}")"
    if [[ -z "${path}" ]]; then
        echo "  SKIP: ${label} (asmdef not found)"
        return 0
    fi

    if asmdef_refs "${path}" | grep -qx "${forbidden}"; then
        echo "  FAIL: ${label}"
        FAIL_COUNT=$((FAIL_COUNT + 1))
        return 1
    else
        echo "  PASS: ${label}"
        PASS_COUNT=$((PASS_COUNT + 1))
        return 0
    fi
}

# check_grep_no_match <pattern> <path> <label> — grep でパターンが 0 件であることを確認
check_grep_no_match() {
    local pattern="$1"
    local path="$2"
    local label="$3"

    if [[ ! -e "${path}" ]]; then
        echo "  SKIP: ${label} (path not found)"
        return 0
    fi

    local count
    count="$(grep -rln "${pattern}" "${path}" 2>/dev/null | wc -l | tr -d ' ')"
    if [[ "${count}" -gt 0 ]]; then
        echo "  FAIL: ${label} (matched in ${count} file(s))"
        FAIL_COUNT=$((FAIL_COUNT + 1))
        return 1
    else
        echo "  PASS: ${label}"
        PASS_COUNT=$((PASS_COUNT + 1))
        return 0
    fi
}

# -----------------------------------------------------------------------------
# Phase 0.5 skeleton mode: only print plan, exit 0
# -----------------------------------------------------------------------------

if [[ "${SKELETON_ONLY}" == "1" ]]; then
    cat <<'EOF'
============================================================
 verify-asmdef-boundaries.sh — Phase 0.5 SKELETON MODE
============================================================
This script is a skeleton. Real rules will be enabled in
Phase 1G after asmdef restructuring is complete.

Planned rules (Phase 1G activation):

[Hard rules]
- SharedKernel.asmdef         references = []  (no UnityEngine, no R3.Unity)
- NodeCatalog.Contracts       no UnityEngine.* references
- Audio.*                     no ref to {Ableton.*, OscMidi.*, Scene.*, UI.*}
- Ableton.Contracts/Session   no ref to OscMidi.*
- Interaction                 no ref to Graph.*
- UI.Presentation             no ref to Graph.* nor NodeCatalog.Runtime
- Nodes.Defaults              no ref to Nodes.{Standard,Audio,OscMidi,Ableton,Scene}
- Nodes.<system>              no ref to *.Analysis/Transport/Session/GraphAdapter
- VContainer                  only Bootstrap can reference

[Origin consistency (single tests via Roslyn/reflection)]
- Interaction.GraphAdapter    emitted commands have Origin == Interaction
- UI.GraphAdapter             emitted commands have Origin == Ui

[Soft warnings]
- Bootstrap/Installers/*.cs   if/switch occurrence threshold
- Bootstrap/EntryPoints/*.cs  must be ITickable adapter only
- Same command kind emitted with 2+ Origin values → adapter duplication warning

To enable real rules:
  bash scripts/verify-asmdef-boundaries.sh --enable
  # or set SKELETON_ONLY=0
============================================================
EOF
    exit 0
fi

# -----------------------------------------------------------------------------
# Real rules (Phase 1G activation)
# -----------------------------------------------------------------------------

echo "============================================================"
echo " verify-asmdef-boundaries.sh — ENABLED MODE"
echo "============================================================"

# --- SharedKernel rules ---
echo ""
echo "[SharedKernel]"
# SharedKernel.asmdef は references = [] であるべき
SHAREDKERNEL_PATH="$(find_asmdef 'Rhizomode.SharedKernel')"
if [[ -n "${SHAREDKERNEL_PATH}" ]]; then
    refs_count="$(asmdef_refs "${SHAREDKERNEL_PATH}" | wc -l | tr -d ' ')"
    if [[ "${refs_count}" -eq 0 ]]; then
        echo "  PASS: SharedKernel.asmdef references = []"
        PASS_COUNT=$((PASS_COUNT + 1))
    else
        echo "  FAIL: SharedKernel.asmdef has ${refs_count} references (expected 0)"
        FAIL_COUNT=$((FAIL_COUNT + 1))
    fi
fi
check_grep_no_match "using UnityEngine" "${ASSETS_RUNTIME}/SharedKernel" \
    "SharedKernel has no UnityEngine import"
check_grep_no_match "using R3" "${ASSETS_RUNTIME}/SharedKernel" \
    "SharedKernel has no R3 import"

# --- NodeCatalog.Contracts rules ---
echo ""
echo "[NodeCatalog.Contracts]"
check_grep_no_match "UnityEngine" "${ASSETS_RUNTIME}/NodeCatalog/Contracts" \
    "NodeCatalog.Contracts has no UnityEngine references"

# --- Audio cross-system ---
echo ""
echo "[Audio cross-system]"
for forbidden in Rhizomode.Ableton.Contracts Rhizomode.Ableton.Transport Rhizomode.Ableton.Session Rhizomode.Ableton.GraphAdapter \
                 Rhizomode.OscMidi.Contracts Rhizomode.OscMidi.Transport Rhizomode.OscMidi.GraphAdapter \
                 Rhizomode.Scene.Contracts Rhizomode.Scene.Runtime Rhizomode.Scene.GraphAdapter \
                 Rhizomode.UI.Contracts Rhizomode.UI.Presentation Rhizomode.UI.GraphAdapter; do
    check_asmdef_no_ref Rhizomode.Audio.Contracts  "${forbidden}" || true
    check_asmdef_no_ref Rhizomode.Audio.Analysis   "${forbidden}" || true
    check_asmdef_no_ref Rhizomode.Audio.GraphAdapter "${forbidden}" || true
done

# --- Ableton.Contracts / Session には OscMidi 禁止 ---
echo ""
echo "[Ableton Contracts/Session: no OscMidi]"
check_asmdef_no_ref Rhizomode.Ableton.Contracts Rhizomode.OscMidi.Contracts || true
check_asmdef_no_ref Rhizomode.Ableton.Contracts Rhizomode.OscMidi.Transport || true
check_asmdef_no_ref Rhizomode.Ableton.Session   Rhizomode.OscMidi.Contracts || true
check_asmdef_no_ref Rhizomode.Ableton.Session   Rhizomode.OscMidi.Transport || true

# --- Interaction には Graph.* 禁止 ---
echo ""
echo "[Interaction: no Graph.*]"
for g in Rhizomode.Graph.Model Rhizomode.Graph.Snapshot Rhizomode.Graph.Events Rhizomode.Graph.Query \
         Rhizomode.Graph.CatalogBridge Rhizomode.Graph.Runtime Rhizomode.Graph.Serialization Rhizomode.Graph.Mutation; do
    check_asmdef_no_ref Rhizomode.Interaction "${g}" || true
done

# --- UI.Presentation には Graph.* / NodeCatalog.Runtime 禁止 ---
echo ""
echo "[UI.Presentation: no Graph.*, no NodeCatalog.Runtime]"
for g in Rhizomode.Graph.Model Rhizomode.Graph.Snapshot Rhizomode.Graph.Events Rhizomode.Graph.Query \
         Rhizomode.Graph.CatalogBridge Rhizomode.Graph.Runtime Rhizomode.Graph.Serialization Rhizomode.Graph.Mutation \
         Rhizomode.NodeCatalog.Runtime; do
    check_asmdef_no_ref Rhizomode.UI.Presentation "${g}" || true
done

# --- Nodes.Defaults ⊄ Nodes.* ---
echo ""
echo "[Nodes.Defaults: no Nodes.*]"
for n in Rhizomode.Nodes.Standard Rhizomode.Nodes.Audio Rhizomode.Nodes.OscMidi \
         Rhizomode.Nodes.Ableton Rhizomode.Nodes.Scene; do
    check_asmdef_no_ref Rhizomode.Nodes.Defaults "${n}" || true
done

# --- VContainer は Bootstrap のみ ---
echo ""
echo "[VContainer: Bootstrap only]"
# Bootstrap 以外で VContainer を参照している asmdef を検出
for asmdef in $(find "${ASSETS_RUNTIME}" "${ASSETS_EDITOR}" -name '*.asmdef' -type f 2>/dev/null); do
    name="$(basename "${asmdef}" .asmdef)"
    if [[ "${name}" == "Rhizomode.Bootstrap" ]]; then continue; fi
    if asmdef_refs "${asmdef}" | grep -qE '^VContainer'; then
        echo "  FAIL: ${name} references VContainer (only Bootstrap allowed)"
        FAIL_COUNT=$((FAIL_COUNT + 1))
    fi
done
# 1 件もエラーがなければ pass
echo "  (scan complete)"

# -----------------------------------------------------------------------------
# Summary
# -----------------------------------------------------------------------------

echo ""
echo "============================================================"
echo " Summary: ${PASS_COUNT} pass, ${FAIL_COUNT} fail"
echo "============================================================"

if [[ "${FAIL_COUNT}" -gt 0 ]]; then
    exit 1
fi
exit 0
