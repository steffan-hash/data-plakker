#!/usr/bin/env bash
# vault-sync.sh -- fail-open SessionEnd hook: lands this project's Log.md
# into the vault's inbox (.raw/_inbox/) so the vault auto-ingests it at its
# next session start, without Steffan doing anything by hand.
#
# Part of the CLAUDE.md "vaste werkwijze" template -- this script itself is
# identical across projects and safe to copy unchanged, same as CLAUDE.md.
#
# Vault location resolution (first match wins):
#   1. $VAULT_PATH env var
#   2. .claude/vault-path (local file, git-ignored -- one line, the path)
#   3. ../me-steffan (sibling directory, the default from setup)
# Steffan works from more than one machine, so a missing vault here is a
# silent no-op, not an error.
#
# Only syncs when Log.md actually changed since the last sync (tracked in
# .claude/.vault-sync-state, local/git-ignored) so re-running doesn't pile
# up duplicate inbox files every session.
#
# Kill switch: VAULT_SYNC=0
# Approved by Steffan on 2026-09-11 (this exact cross-repo commit+push
# pattern was confirmed explicitly, since it pushes to another repo).

main() {
  set -u
  if [ "${VAULT_SYNC:-1}" = "0" ]; then return 0; fi
  cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || return 0

  [ -f "./Log.md" ] || return 0

  local vault=""
  if [ -n "${VAULT_PATH:-}" ] && [ -d "${VAULT_PATH:-}" ]; then
    vault="$VAULT_PATH"
  elif [ -f "./.claude/vault-path" ]; then
    local p
    p="$(cat ./.claude/vault-path 2>/dev/null)"
    if [ -n "$p" ] && [ -d "$p" ]; then
      vault="$p"
    fi
  fi
  if [ -z "$vault" ] && [ -d "../me-steffan" ]; then
    vault="../me-steffan"
  fi
  [ -n "$vault" ] || return 0
  [ -d "$vault/.raw/_inbox" ] || return 0

  # Skip if Log.md hasn't changed since the last sync.
  local state="./.claude/.vault-sync-state"
  local cur_hash
  cur_hash="$(git hash-object ./Log.md 2>/dev/null)" || return 0
  if [ -f "$state" ] && [ "$(cat "$state" 2>/dev/null)" = "$cur_hash" ]; then
    return 0
  fi

  local proj_name
  proj_name="$(basename "$(pwd)")"
  local stamp
  stamp="$(date +%Y-%m-%d-%H%M%S)"
  local dest_name="${proj_name}-log-${stamp}.md"
  local dest="$vault/.raw/_inbox/$dest_name"

  {
    echo "<!-- Bron: Log.md uit repo '${proj_name}', automatisch gesynchroniseerd bij sessie-einde. -->"
    echo ""
    cat ./Log.md
  } > "$dest" 2>/dev/null || return 0

  mkdir -p ./.claude 2>/dev/null
  printf '%s' "$cur_hash" > "$state" 2>/dev/null

  # Best-effort: commit + push the landed file in the vault repo too, so
  # the raw material is durable even if Steffan doesn't open the vault for
  # a while. The vault's own SessionStart hook still does the real ingest
  # into wiki/ next time a vault session starts.
  (
    cd "$vault" 2>/dev/null || exit 0
    git add -- ".raw/_inbox/$dest_name" 2>/dev/null
    git diff --cached --quiet 2>/dev/null && exit 0
    git commit -q -m "vault: inbox drop from ${proj_name} (auto-sync)" >/dev/null 2>&1
    ( git push >/dev/null 2>&1 & ) >/dev/null 2>&1
  )

  return 0
}

main
exit 0
