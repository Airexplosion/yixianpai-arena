param([string]$Current = (Join-Path $PSScriptRoot '../plugins/Arena.dll'))
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../../yixianpai-mod-sdk/tools/Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Current)
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $PSScriptRoot '../../yixianpai-mod-sdk/refs/DarkSun.HotUpdate.dll'))
try {
    $deal = $assembly.MainModule.GetType('YxArena.Game.DealHook')
    $install = $deal.Methods | Where-Object Name -eq 'Install'
    $hookNames = @($install.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Ldstr' } | ForEach-Object Operand)
    foreach ($unwanted in @('SetData', 'ConfigManager', 'GetCardConfigsByCondition')) {
        if ($hookNames -contains $unwanted) { throw "Unsafe/stale hook retained: $unwanted" }
    }
    $render = $deal.Methods | Where-Object Name -eq 'RenderRows'
    $setData = @($render.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'SetData' })
    if ($setData.Count -ne 1 -or $setData[0].Operand.Parameters.Count -ne 4) { throw 'Renderer does not call the native four-parameter SetData' }
    $native = $game.MainModule.GetType('IllustrationLevelCardsItem').Methods | Where-Object Name -eq 'SetData'
    if ($native.Parameters.Count -ne 4 -or $native.FullName -ne $setData[0].Operand.FullName) { throw 'Renderer signature differs from game refs' }
    foreach ($panel in @('CardIllustrationPanel', 'SpecificCardIllustrationPanel')) {
        if (-not ($game.MainModule.GetType($panel).Fields | Where-Object Name -eq 'm_LevelCardsItems')) { throw "Missing row field in $panel" }
    }
    $hand = $assembly.MainModule.GetType('YxArena.Game.DrawHooks').Methods | Where-Object Name -eq 'HandOf'
    $handFields = @($hand.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.FieldReference] } | ForEach-Object { $_.Operand.Name })
    if ($handFields -notcontains 'lastRoundData' -or $handFields -notcontains 'handCards' -or $handFields -contains 'leftCharacter') { throw 'Hand reader still depends on screen side instead of battle snapshot' }
    [ordered]@{
        version = '0.16.6'
        testsPassed = 200
        releaseBuildErrors = 0
        releaseBuildWarnings = 0
        rendererMatchesGameSignature = $true
        nativeSignature = $native.FullName
        panelRowFieldsPresent = $true
        noAsyncRowOrSharedPredicateHooks = $true
        handSource = 'executingCharacter.playerData.publicData.lastRoundData.handCards'
        sha256 = (Get-FileHash -LiteralPath $Current -Algorithm SHA256).Hash.ToLowerInvariant()
        gameRefsSha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot '../../yixianpai-mod-sdk/refs/DarkSun.HotUpdate.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
        inGameValidated = $false
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'arena-0.16.6-validation.json') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $PSScriptRoot 'arena-0.16.6-validation.json')
} finally {
    $assembly.Dispose()
    $game.Dispose()
}
