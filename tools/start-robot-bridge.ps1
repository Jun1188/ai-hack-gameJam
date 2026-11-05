param([switch]$FallbackOnly)
$ErrorActionPreference = 'Stop'
$bridgeArgs = @((Join-Path $PSScriptRoot 'robot_bridge.py'))
if ($FallbackOnly) { $bridgeArgs += '--fallback-only' }
& python @bridgeArgs
exit $LASTEXITCODE
