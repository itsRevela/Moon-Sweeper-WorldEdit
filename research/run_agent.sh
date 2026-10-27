#!/usr/bin/env bash
# Runs one DeepSeek research agent headless with read-only tools (Read, Grep, Glob): no shell, no edits.
# Same settings as the DeepSeek claude-deepseek.bat launcher but without --dangerously-skip-permissions.
# Usage (from anywhere): research/run_agent.sh research/briefs/<brief>.md research/reports/<report>.md
set -euo pipefail
DS="${DEEPSEEK_DIR:?set DEEPSEEK_DIR to the folder holding apikey.txt}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
brief="$1"
report="$2"
[ -f "$brief" ] || { echo "no brief: $brief" >&2; exit 1; }
[ -s "$DS/apikey.txt" ] || { echo "DeepSeek API key file missing or empty" >&2; exit 1; }

export ANTHROPIC_AUTH_TOKEN="$(tr -d '\r\n' < "$DS/apikey.txt")"
unset ANTHROPIC_API_KEY
export ANTHROPIC_BASE_URL="https://api.deepseek.com/anthropic"
export ANTHROPIC_MODEL="deepseek-flash[1m]"
export ANTHROPIC_DEFAULT_OPUS_MODEL="deepseek-flash[1m]"
export ANTHROPIC_DEFAULT_SONNET_MODEL="deepseek-flash[1m]"
export ANTHROPIC_DEFAULT_HAIKU_MODEL="deepseek-flash"
export CLAUDE_CODE_SUBAGENT_MODEL="deepseek-flash"
export CLAUDE_CODE_EFFORT_LEVEL="max"

claude -p "$(cat "$brief")" --allowedTools "Read,Grep,Glob" --output-format text > "$report"
echo "report written: $report ($(wc -w < "$report") words)"
