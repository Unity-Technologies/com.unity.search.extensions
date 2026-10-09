# Shared paths for the version-matrix scripts. Sourced, not executed.
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
PROJECT="$REPO/projects/SearchExtensionsExamples"
PROJECT_WIN="$(cygpath -m "$PROJECT")"
EDITORS="/c/Program Files/Unity/Hub/Editor"
LOG_DIR="$PROJECT/Logs/version-matrix"
mkdir -p "$LOG_DIR"
