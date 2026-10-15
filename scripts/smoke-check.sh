#!/usr/bin/env bash
# Post-deploy smoke check for PDS. Needs bash, curl and jq.
#
#   scripts/smoke-check.sh <site-url> [<api-base-url>]
#
# api-base-url defaults to the apiBaseUrl in <site-url>/config.json.
# Env:
#   PDS_TOKEN         Cognito ID token for an Admin or Manager user (the API authorizer
#                     checks the app client, so use the ID token). Enables the signed-in checks.
#                     Never printed. Against local dev (Auth:Mode=Development) any value or none works.
#   SMOKE_DOCUMENTS=1 Also run the Documents upload round trip. WRITES DATA (one small document;
#                     soft-deleted at the end only if the token is Admin). Needs SMOKE_PROJECT_ID.
#   SMOKE_PROJECT_ID  Project id for project-scoped checks. Defaults to the first project listed.
set -euo pipefail

SITE=${1:-}
[[ -n "$SITE" ]] || { echo "usage: $0 <site-url> [<api-base-url>]" >&2; exit 2; }
SITE=${SITE%/}
API=${2:-}
API=${API%/}

command -v curl >/dev/null && command -v jq >/dev/null || { echo "curl and jq are required" >&2; exit 2; }

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
AUTH_HDR="$WORK/auth"
if [[ -n "${PDS_TOKEN:-}" ]]; then
  printf 'Authorization: Bearer %s\n' "$PDS_TOKEN" >"$AUTH_HDR"   # header file keeps the token out of argv and output
else
  : >"$AUTH_HDR"
fi

PASS=0 FAIL=0 SKIP=0
pass() { PASS=$((PASS + 1)); echo "PASS  $1"; }
fail() { FAIL=$((FAIL + 1)); echo "FAIL  $1${2:+ ($2)}"; }
skip() { SKIP=$((SKIP + 1)); echo "SKIP  $1${2:+ ($2)}"; }

# req <method> <url> [authed=1|0] [curl args...] -> sets CODE, writes body to $WORK/body, headers to $WORK/head
req() {
  local method=$1 url=$2 authed=${3:-1}; shift 3 || shift $#
  local -a hdr=()
  [[ $authed == 1 && -s $AUTH_HDR ]] && hdr=(-H "@$AUTH_HDR")
  CODE=$(curl -sS -o "$WORK/body" -D "$WORK/head" -w '%{http_code}' --max-time 30 -X "$method" ${hdr[@]+"${hdr[@]}"} "$@" "$url" 2>"$WORK/err") || CODE=000
}

# expect <label> <want-code> <method> <url> [authed] [curl args...]
expect() {
  local label=$1 want=$2; shift 2
  req "$@"
  if [[ $CODE == "$want" ]]; then pass "$label"; return 0; fi
  fail "$label" "HTTP $CODE, wanted $want$([[ $CODE == 000 ]] && echo ": $(head -c 200 "$WORK/err")")"
  return 1
}

echo "Site: $SITE"

# ---- anonymous checks -------------------------------------------------------------------------
if expect "SPA loads" 200 GET "$SITE/" 0; then
  grep -qi '<html' "$WORK/body" && pass "SPA returns HTML" || fail "SPA returns HTML" "no <html> in body"
fi

if expect "/config.json" 200 GET "$SITE/config.json" 0; then
  # Empty is fine for local dev, where the SPA proxies /api; deployed sites carry the API URL.
  CFG_API=$(jq -r '.apiBaseUrl // empty' "$WORK/body" 2>/dev/null || true)
  jq -e 'has("apiBaseUrl")' "$WORK/body" >/dev/null 2>&1 \
    && pass "/config.json has apiBaseUrl" || fail "/config.json has apiBaseUrl"
  [[ -n "$API" ]] || API=${CFG_API%/}
fi

if [[ -z "$API" ]]; then
  # Local dev: the SPA proxies /api to the API, so the site URL is the API base.
  API=$SITE
  echo "No api-base-url found; using the site URL"
fi
echo "API:  $API"

# A 200 here proves the Lambda started: Locale:TimeZone and the other options validate at startup.
if expect "GET /api/config (API started, options valid)" 200 GET "$API/api/config" 0; then
  jq -e '.timeZone and .currency' "$WORK/body" >/dev/null 2>&1 \
    && pass "/api/config has timeZone and currency" || fail "/api/config has timeZone and currency"
fi

if [[ -z "${PDS_TOKEN:-}" ]]; then
  # Deployed APIs reject this at the gateway; local dev auth accepts everything, so 200 is OK there.
  req GET "$API/api/me" 0
  case $CODE in
    401) pass "GET /api/me without a token is 401" ;;
    200) skip "GET /api/me without a token is 401" "got 200: Auth:Mode=Development accepts any request" ;;
    *)   fail "GET /api/me without a token is 401" "HTTP $CODE" ;;
  esac
  echo "No PDS_TOKEN set: skipping signed-in checks"
else
  req GET "$API/api/me" 0
  [[ $CODE == 401 ]] && pass "GET /api/me without a token is 401" \
    || skip "GET /api/me without a token is 401" "got $CODE: Auth:Mode=Development accepts any request"

  # ---- signed-in checks -----------------------------------------------------------------------
  ROLES=
  if expect "GET /api/me" 200 GET "$API/api/me"; then
    ROLES=$(jq -r '.roles | join(",")' "$WORK/body")
    echo "      signed in with roles: $ROLES"
  fi

  expect "GET /api/portfolio/projects" 200 GET "$API/api/portfolio/projects" && \
    PROJECT_FIRST=$(jq -r '.items[0].id // empty' "$WORK/body" 2>/dev/null || true)
  PROJECT=${SMOKE_PROJECT_ID:-${PROJECT_FIRST:-}}

  expect "GET /api/contractors" 200 GET "$API/api/contractors" || true
  expect "GET /api/consents/due" 200 GET "$API/api/consents/due" || true
  expect "GET /api/sales/summary" 200 GET "$API/api/sales/summary" || true
  if [[ -n "$PROJECT" ]]; then
    expect "GET risks (project)" 200 GET "$API/api/risks/projects/$PROJECT/risks" || true
    expect "GET costs (project)" 200 GET "$API/api/costs/projects/$PROJECT" || true
    expect "GET sales units (project)" 200 GET "$API/api/sales/projects/$PROJECT/units" || true
    expect "GET documents (project)" 200 GET "$API/api/documents/projects/$PROJECT/documents" || true
    expect "GET cashflow (project)" 200 GET "$API/api/cashflow/projects/$PROJECT" || true
  else
    for m in risks costs "sales units" documents cashflow; do
      skip "GET $m (project)" "no project exists yet; create one or set SMOKE_PROJECT_ID"
    done
  fi

  # Reporting: list datasets, download the first one, check for the UTF-8 BOM Excel needs.
  if expect "GET /api/reporting/exports" 200 GET "$API/api/reporting/exports"; then
    DATASET=$(jq -r '.[0].name // empty' "$WORK/body")
    if [[ -z "$DATASET" ]]; then
      fail "reporting export lists a dataset"
    elif expect "GET /api/reporting/exports/$DATASET" 200 GET "$API/api/reporting/exports/$DATASET"; then
      [[ $(head -c 3 "$WORK/body" | od -An -tx1 | tr -d ' \n') == efbbbf ]] \
        && pass "CSV starts with UTF-8 BOM" || fail "CSV starts with UTF-8 BOM"
      grep -qi '^content-type: *text/csv' "$WORK/head" \
        && pass "CSV content type is text/csv" || fail "CSV content type is text/csv"
    fi
  fi

  # ---- optional Documents round trip (writes data) --------------------------------------------
  if [[ "${SMOKE_DOCUMENTS:-}" == 1 ]]; then
    if [[ -z "${SMOKE_PROJECT_ID:-}" ]]; then
      fail "Documents round trip" "SMOKE_PROJECT_ID is required"
    else
      DOCS="$API/api/documents/projects/$SMOKE_PROJECT_ID/documents"
      PAYLOAD="$WORK/payload.txt"
      printf 'pds smoke check %s\n' "$(date -u +%FT%TZ)" >"$PAYLOAD"
      SIZE=$(wc -c <"$PAYLOAD" | tr -d ' ')
      JSON=(-H 'Content-Type: application/json')
      if expect "Documents: request upload URL" 200 POST "$DOCS/uploads" 1 "${JSON[@]}" \
           -d "$(jq -nc --argjson s "$SIZE" '{fileName:"smoke-check.txt",sizeBytes:$s}')"; then
        UPLOAD_ID=$(jq -r .uploadId "$WORK/body"); URL=$(jq -r .uploadUrl "$WORK/body"); CT=$(jq -r .contentType "$WORK/body")
        # Local storage returns a path relative to the API; S3 returns an absolute presigned URL (no auth header).
        [[ $URL == http* ]] || URL=$API$URL
        if expect "Documents: PUT file" 200 PUT "$URL" 0 -H "Content-Type: $CT" --data-binary "@$PAYLOAD" \
           && expect "Documents: confirm (create document)" 201 POST "$DOCS" 1 "${JSON[@]}" \
             -d "$(jq -nc --arg u "$UPLOAD_ID" '{uploadId:$u,fileName:"smoke-check.txt",title:"Smoke check (safe to delete)",category:"Other"}')"; then
          DOC_ID=$(jq -r .id "$WORK/body"); DOC_VER=$(jq -r .version "$WORK/body")
          if expect "Documents: download link" 200 GET "$DOCS/$DOC_ID/versions/1/download"; then
            DL=$(jq -r .url "$WORK/body"); [[ $DL == http* ]] || DL=$API$DL
            if expect "Documents: download file" 200 GET "$DL" 0; then
              cmp -s "$WORK/body" "$PAYLOAD" && pass "Documents: downloaded bytes match" || fail "Documents: downloaded bytes match"
            fi
          fi
          if [[ ",$ROLES," == *,Admin,* ]]; then
            expect "Documents: admin delete" 204 POST "$DOCS/$DOC_ID/delete" 1 "${JSON[@]}" -d "{\"version\":$DOC_VER}" || true
          else
            skip "Documents: admin delete" "not an Admin; remove document $DOC_ID by hand"
          fi
        fi
      fi
    fi
  else
    skip "Documents round trip" "opt in with SMOKE_DOCUMENTS=1 and SMOKE_PROJECT_ID (writes data)"
  fi
fi

echo
echo "Result: $PASS passed, $FAIL failed, $SKIP skipped"
[[ $FAIL -eq 0 ]]
