$ErrorActionPreference = 'Stop'
$arenaRoot = Split-Path -Parent $PSScriptRoot
$sdkRoot = Join-Path (Split-Path -Parent $arenaRoot) 'yixianpai-mod-sdk'
Add-Type -Path (Join-Path $sdkRoot 'tools/Mono.Cecil.dll')
$refDir = Join-Path $PSScriptRoot 'sdk-ref/net40'
New-Item -ItemType Directory -Path $refDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $sdkRoot 'sdk/net40/Yx.ModSdk.dll') -Destination $refDir
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $sdkRoot 'sdk/net40/Yx.ModSdk.Core.dll'))
$module = $assembly.MainModule
# Compile-only facade for the LocalPractice constructor already used by the
# verified 0.16.0 Arena.dll. Never deploy these SDK reference DLLs.
if ($null -eq $module.GetType('Yx.ModSdk.LocalPractice')) {
    $type = [Mono.Cecil.TypeDefinition]::new('Yx.ModSdk', 'LocalPractice', [Mono.Cecil.TypeAttributes]::Public -bor [Mono.Cecil.TypeAttributes]::Sealed, $module.TypeSystem.Object)
    $flags = [Mono.Cecil.MethodAttributes]([int][Mono.Cecil.MethodAttributes]::Public -bor [int][Mono.Cecil.MethodAttributes]::HideBySig -bor [int][Mono.Cecil.MethodAttributes]::SpecialName -bor [int][Mono.Cecil.MethodAttributes]::RTSpecialName)
    $ctor = [Mono.Cecil.MethodDefinition]::new('.ctor', $flags, $module.TypeSystem.Void)
    $func = [Mono.Cecil.TypeReference]::new('System', 'Func`1', $module, $module.TypeSystem.CoreLibrary)
    foreach ($resultType in @($module.TypeSystem.Boolean, $module.TypeSystem.String)) {
        $argType = [Mono.Cecil.GenericInstanceType]::new($func)
        $argType.GenericArguments.Add($resultType)
        $ctor.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($argType))
    }
    $ctor.Body.GetILProcessor().Append([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ret))
    $type.Methods.Add($ctor)
    $module.Types.Add($type)
}
$contextType = $module.GetType('Yx.ModSdk.ModContext')
if ($null -eq ($contextType.Properties | Where-Object Name -eq 'Lang')) {
    $getterFlags = [Mono.Cecil.MethodAttributes]([int][Mono.Cecil.MethodAttributes]::Public -bor [int][Mono.Cecil.MethodAttributes]::HideBySig -bor [int][Mono.Cecil.MethodAttributes]::SpecialName)
    $getter = [Mono.Cecil.MethodDefinition]::new('get_Lang', $getterFlags, $module.TypeSystem.String)
    $getter.Body.GetILProcessor().Append([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldstr, 'zh'))
    $getter.Body.GetILProcessor().Append([Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ret))
    $property = [Mono.Cecil.PropertyDefinition]::new('Lang', [Mono.Cecil.PropertyAttributes]::None, $module.TypeSystem.String)
    $property.GetMethod = $getter
    $contextType.Methods.Add($getter)
    $contextType.Properties.Add($property)
}
$assembly.Write((Join-Path $refDir 'Yx.ModSdk.Core.dll'))
$assembly.Dispose()
