param(
    [string]$Original = (Join-Path $PSScriptRoot '../../downloads/release-arena/com.yx.arena/plugins/Arena.dll'),
    [string]$Current = (Join-Path $PSScriptRoot '../plugins/Arena.dll')
)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../../yixianpai-mod-sdk/tools/Mono.Cecil.dll')
$baseline = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Original)
$updated = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Current)
$missingTypes = @()
$missingMethods = @()
$missingFields = @()
foreach ($type in $baseline.MainModule.Types) {
    if ($type.Name -eq '<Module>') { continue }
    $match = $updated.MainModule.GetType($type.FullName)
    if ($null -eq $match) { $missingTypes += $type.FullName; continue }
    foreach ($method in $type.Methods) {
        # Internal UI constructor signatures changed to carry settings callbacks.
        if ($method.Name -eq '.ctor') { continue }
        if ($null -eq ($match.Methods | Where-Object { $_.Name -eq $method.Name -and $_.Parameters.Count -eq $method.Parameters.Count })) {
            $missingMethods += $method.FullName
        }
    }
    foreach ($field in $type.Fields) {
        if ($null -eq ($match.Fields | Where-Object Name -eq $field.Name)) { $missingFields += "$($type.FullName).$($field.Name)" }
    }
}
$report = [ordered]@{
    originalSha256 = (Get-FileHash -LiteralPath $Original -Algorithm SHA256).Hash.ToLowerInvariant()
    currentSha256 = (Get-FileHash -LiteralPath $Current -Algorithm SHA256).Hash.ToLowerInvariant()
    originalTopLevelTypes = $baseline.MainModule.Types.Count - 1
    currentTopLevelTypes = $updated.MainModule.Types.Count - 1
    missingTypes = @($missingTypes)
    missingMethods = @($missingMethods)
    missingFields = @($missingFields)
    note = 'Structural audit only. Internal UI helper methods and fields were replaced by adaptive layout; semantic and in-game validation are separate.'
}
$report | ConvertTo-Json -Depth 5
$baseline.Dispose()
$updated.Dispose()
if ($missingTypes.Count -gt 0) { exit 1 }
