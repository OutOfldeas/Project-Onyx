#!/bin/bash
# Kompiliert alle Onyx-Assemblies gegen die Unity-Bibliotheken, ohne Unity zu starten.
# Findet Compilerfehler im Unity-Code (Game, Net, Editor), z. B. in einer Cloud-Sitzung.
# Nicht abgedeckt: Netcode-Codegenerierung (RPC-Regeln) und alles, was erst zur Laufzeit passiert.
#
# Benötigt das .NET-SDK (csc) und Kopien der Unity-Bibliotheken (nicht im Repo, Unity-Lizenz):
#   UNITY_MANAGED      …/Editor/Data/Managed/UnityEngine   (UnityEngine.*.dll, UnityEditor*.dll)
#   UNITY_NETSTANDARD  …/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll
#   SCRIPT_ASSEMBLIES  Onyx/Library/ScriptAssemblies       (Pakete: Netcode, Input System, URP …)
# Aufruf:  tools/UnityCompileCheck/compile.sh      (Rückgabewert 0 = alles kompiliert)
set -u
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
SRC=${SRC:-$ROOT/Onyx/Assets/_Project}
: "${UNITY_MANAGED:?UNITY_MANAGED setzen}" "${UNITY_NETSTANDARD:?UNITY_NETSTANDARD setzen}" "${SCRIPT_ASSEMBLIES:?SCRIPT_ASSEMBLIES setzen}"
OUT=${OUT:-/tmp/onyx-compile-check}; mkdir -p "$OUT"; rm -f "$OUT"/*.dll

CSC_DLL=$(ls -d /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll /usr/share/dotnet/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | tail -1)
CSC="dotnet $CSC_DLL"
refs() { ls "$@" 2>/dev/null | sed 's/^/-r:/' | tr '\n' ' '; }
ENGINE=$(refs "$UNITY_MANAGED"/UnityEngine.*.dll)
EDITOR=$(refs "$UNITY_MANAGED"/UnityEditor*.dll)
PKG=$(refs "$SCRIPT_ASSEMBLIES"/Unity.*.dll "$SCRIPT_ASSEMBLIES"/UnityEngine.UI.dll)
COMMON="-nologo -noconfig -nostdlib -langversion:9.0 -target:library -nowarn:1591,0649,0414 -r:$UNITY_NETSTANDARD"

rc=0
build() {
  local name=$1 dir=$2; shift 2
  $CSC $COMMON -out:"$OUT/$name.dll" "$@" $(find "$SRC/$dir" -name '*.cs') 2>&1 | grep -E "error|warning" | sed "s|$SRC/||"
  if [ -f "$OUT/$name.dll" ]; then echo "ok    $name"; else echo "FEHLER $name"; rc=1; fi
}
build Onyx.Core   Scripts/Core
build Onyx.Net    Scripts/Net    -r:"$OUT/Onyx.Core.dll" $ENGINE $PKG
build Onyx.Game   Scripts/Game   -r:"$OUT/Onyx.Core.dll" -r:"$OUT/Onyx.Net.dll" $ENGINE $PKG
build Onyx.Editor Scripts/Editor -define:UNITY_EDITOR -r:"$OUT/Onyx.Core.dll" -r:"$OUT/Onyx.Net.dll" -r:"$OUT/Onyx.Game.dll" $ENGINE $PKG $EDITOR
exit $rc
